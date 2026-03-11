using System.Text.Json;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Supervisor;

namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Implements daemon-stop command workflow orchestration. </summary>
internal sealed class DaemonStopCommandService : IDaemonStopCommandService
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly SupervisorManifestStore supervisorManifestStore;

    private readonly SupervisorClient supervisorClient;

    private readonly IDaemonStopOperation daemonStopOperation;

    /// <summary> Initializes a new instance of the <see cref="DaemonStopCommandService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command execution-context resolver dependency. </param>
    /// <param name="daemonStopOperation"> The daemon stop-operation dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonStopCommandService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        SupervisorManifestStore supervisorManifestStore,
        SupervisorClient supervisorClient,
        IDaemonStopOperation daemonStopOperation)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.supervisorManifestStore = supervisorManifestStore ?? throw new ArgumentNullException(nameof(supervisorManifestStore));
        this.supervisorClient = supervisorClient ?? throw new ArgumentNullException(nameof(supervisorClient));
        this.daemonStopOperation = daemonStopOperation ?? throw new ArgumentNullException(nameof(daemonStopOperation));
    }

    /// <summary> Executes one daemon-stop workflow. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> option value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> option value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-stop execution result. </returns>
    public async ValueTask<DaemonStopExecutionResult> Stop (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await daemonCommandExecutionContextResolver.Resolve(
                UcliCommandIds.DaemonStop,
                projectPath,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return DaemonStopExecutionResult.Failure(contextResult.Error!);
        }

        var executionContext = contextResult.Context!;
        var deadline = ExecutionDeadline.Start(executionContext.Timeout);
        var stopResult = await TryStopViaSupervisor(executionContext, deadline, cancellationToken).ConfigureAwait(false);
        if (stopResult == null)
        {
            if (!deadline.TryGetRemainingTimeout(out var directStopTimeout))
            {
                return DaemonStopExecutionResult.Failure(ExecutionError.Timeout(
                    "Timed out before daemon stop fallback could begin."));
            }

            stopResult = await daemonStopOperation.Stop(
                    executionContext.Context.UnityProject,
                    directStopTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!stopResult.IsSuccess)
        {
            return DaemonStopExecutionResult.Failure(stopResult.Error ?? ExecutionError.InternalError(
                "Daemon stop operation failed without structured error details."));
        }

        if (!DaemonStopStateCodec.TryToValue(stopResult.Status, out var stopStatus))
        {
            return DaemonStopExecutionResult.Failure(ExecutionError.InternalError(
                $"Daemon stop returned unsupported status: {stopResult.Status}."));
        }

        var output = new DaemonStopExecutionOutput(
            StopStatus: stopStatus!,
            DaemonStatus: DaemonStatusStateCodec.NotRunning,
            TimeoutMilliseconds: checked((int)executionContext.Timeout.TotalMilliseconds),
            Session: null);
        return DaemonStopExecutionResult.Success(output);
    }

    private async ValueTask<DaemonStopResult?> TryStopViaSupervisor (
        DaemonCommandExecutionContext executionContext,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var manifestReadTimeout))
        {
            return DaemonStopResult.Failure(ExecutionError.Timeout(
                "Timed out before supervisor manifest read could begin."));
        }

        SupervisorInstanceManifest? manifest;
        try
        {
            manifest = await supervisorManifestStore.ReadOrNull(
                    executionContext.Context.UnityProject.RepositoryRoot,
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
            var cleanupFailure = TryDeleteMalformedSupervisorManifest(
                executionContext.Context.UnityProject.RepositoryRoot);
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
                executionContext.Context.UnityProject,
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