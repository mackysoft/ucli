using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;

/// <summary> Encapsulates supervisor bootstrap, reachability, and project control flows. </summary>
internal sealed class SupervisorProjectGateway : ISupervisorProjectGateway
{
    private readonly SupervisorBootstrapper supervisorBootstrapper;

    private readonly SupervisorManifestStore supervisorManifestStore;

    private readonly SupervisorClient supervisorClient;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="SupervisorProjectGateway" /> class. </summary>
    public SupervisorProjectGateway (
        SupervisorBootstrapper supervisorBootstrapper,
        SupervisorManifestStore supervisorManifestStore,
        SupervisorClient supervisorClient,
        TimeProvider? timeProvider = null)
    {
        this.supervisorBootstrapper = supervisorBootstrapper ?? throw new ArgumentNullException(nameof(supervisorBootstrapper));
        this.supervisorManifestStore = supervisorManifestStore ?? throw new ArgumentNullException(nameof(supervisorManifestStore));
        this.supervisorClient = supervisorClient ?? throw new ArgumentNullException(nameof(supervisorClient));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<DaemonStartResult> EnsureRunning (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!deadline.TryGetRemainingTimeout(out var bootstrapTimeout))
        {
            return DaemonStartResult.Failure(ExecutionError.Timeout(
                "Timed out before supervisor bootstrap could begin."));
        }

        var bootstrapResult = await supervisorBootstrapper.EnsureReady(
                unityProject.RepositoryRoot,
                bootstrapTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!bootstrapResult.IsSuccess)
        {
            return DaemonStartResult.Failure(bootstrapResult.Error!);
        }

        if (!deadline.TryGetRemainingTimeout(out var ensureRunningTimeout))
        {
            return DaemonStartResult.Failure(ExecutionError.Timeout(
                "Timed out before supervisor ensureRunning could begin."));
        }

        return await supervisorClient.EnsureRunning(
                bootstrapResult.Manifest!,
                unityProject,
                ensureRunningTimeout,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DaemonStopResult?> TryStopProject (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!deadline.TryGetRemainingTimeout(out var manifestReadTimeout))
        {
            return DaemonStopResult.Failure(ExecutionError.Timeout(
                "Timed out before supervisor manifest read could begin."));
        }

        SupervisorInstanceManifest? manifest;
        try
        {
            manifest = await supervisorManifestStore.ReadOrNull(
                    unityProject.RepositoryRoot,
                    manifestReadTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            return DaemonStopResult.Failure(ExecutionError.Timeout(exception.Message));
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            var cleanupFailure = TryDeleteMalformedSupervisorManifest(unityProject.RepositoryRoot);
            if (cleanupFailure != null)
            {
                return DaemonStopResult.Failure(cleanupFailure);
            }

            return null;
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return DaemonStopResult.Failure(ExecutionError.InvalidArgument(
                $"Supervisor manifest path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (manifest == null)
        {
            return null;
        }

        if (!deadline.TryGetRemainingTimeout(out var probeBudget))
        {
            return DaemonStopResult.Failure(ExecutionError.Timeout(
                "Timed out before supervisor stop probe could begin."));
        }

        var probeTimeout = probeBudget < SupervisorConstants.PingTimeout
            ? probeBudget
            : SupervisorConstants.PingTimeout;
        var probeStatus = await supervisorClient.ProbeReachability(manifest, probeTimeout, cancellationToken).ConfigureAwait(false);
        if (probeStatus == SupervisorReachabilityProbeStatus.Unreachable)
        {
            return null;
        }

        if (!deadline.TryGetRemainingTimeout(out var stopTimeout))
        {
            return DaemonStopResult.Failure(ExecutionError.Timeout(
                "Timed out before supervisor stopProject could begin."));
        }

        return await supervisorClient.StopProject(
                manifest,
                unityProject,
                stopTimeout,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private ExecutionError? TryDeleteMalformedSupervisorManifest (
        string repositoryRoot)
    {
        try
        {
            supervisorManifestStore.DeleteIfExists(repositoryRoot);
            return null;
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ExecutionError.InvalidArgument(
                $"Supervisor manifest cleanup path is invalid. {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // NOTE:
            // malformed supervisor metadata should not block the direct-stop fallback when only
            // the best-effort manifest cleanup failed.
            return null;
        }
    }
}
