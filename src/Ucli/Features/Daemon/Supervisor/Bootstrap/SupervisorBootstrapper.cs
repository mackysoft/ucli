using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;

/// <summary> Ensures the worktree-local supervisor is running and reachable before daemon commands proceed. </summary>
internal sealed class SupervisorBootstrapper
{
    private const int MaxLaunchAttempts = 2;

    private const int ManifestPublicationGraceProbeCount = 2;

    private const string LaunchFailureMessage =
        "Supervisor launch did not publish a reachable manifest before startup completed.";

    private readonly SupervisorManifestStore manifestStore;

    private readonly SupervisorClient supervisorClient;

    private readonly ISupervisorProcessLauncher processLauncher;

    private readonly SupervisorBootstrapLockProvider bootstrapLockProvider;

    private readonly SupervisorEndpointResolver endpointResolver;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="SupervisorBootstrapper" /> class. </summary>
    /// <param name="manifestStore"> The supervisor manifest-store dependency. </param>
    /// <param name="supervisorClient"> The supervisor client dependency. </param>
    /// <param name="processLauncher"> The supervisor process-launcher dependency. </param>
    /// <param name="bootstrapLockProvider"> The bootstrap-lock provider dependency. </param>
    /// <param name="endpointResolver"> The supervisor endpoint resolver dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    public SupervisorBootstrapper (
        SupervisorManifestStore manifestStore,
        SupervisorClient supervisorClient,
        ISupervisorProcessLauncher processLauncher,
        SupervisorBootstrapLockProvider bootstrapLockProvider,
        SupervisorEndpointResolver endpointResolver,
        TimeProvider? timeProvider = null)
    {
        this.manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        this.supervisorClient = supervisorClient ?? throw new ArgumentNullException(nameof(supervisorClient));
        this.processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        this.bootstrapLockProvider = bootstrapLockProvider ?? throw new ArgumentNullException(nameof(bootstrapLockProvider));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Ensures the supervisor for the specified storage root is running and reachable. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="timeout"> The bootstrap timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The bootstrap result. </returns>
    public async ValueTask<SupervisorBootstrapResult> EnsureReady (
        string storageRoot,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            return SupervisorBootstrapResult.Failure(ExecutionError.InvalidArgument(
                "Supervisor bootstrap storage root must not be empty."));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        var normalizedStorageRoot = Path.GetFullPath(storageRoot);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);

        if (!deadline.TryGetRemainingTimeout(out var lockAcquireTimeout))
        {
            return SupervisorBootstrapResult.Failure(ExecutionError.Timeout(
                "Timed out before supervisor bootstrap could begin."));
        }

        IAsyncDisposable lockHandle;
        try
        {
            lockHandle = await bootstrapLockProvider.Acquire(
                    normalizedStorageRoot,
                    lockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            return SupervisorBootstrapResult.Failure(ExecutionError.Timeout(
                $"Timed out while waiting for supervisor bootstrap lock. {exception.Message}"));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return SupervisorBootstrapResult.Failure(ExecutionError.InvalidArgument(
                $"Supervisor bootstrap path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SupervisorBootstrapResult.Failure(ExecutionError.InternalError(
                $"Failed to acquire supervisor bootstrap lock. {exception.Message}"));
        }

        await using var acquiredLock = lockHandle;
        var launchAttemptCount = 0;
        var manifestMissCountAfterLaunch = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var manifestProbe = await ProbeManifestAvailability(normalizedStorageRoot, deadline, cancellationToken).ConfigureAwait(false);
            if (manifestProbe.ReadyManifest != null)
            {
                return SupervisorBootstrapResult.Success(manifestProbe.ReadyManifest);
            }

            if (manifestProbe.Error != null)
            {
                return SupervisorBootstrapResult.Failure(manifestProbe.Error);
            }

            if (manifestProbe.ShouldLaunchSupervisor)
            {
                var shouldDelayBeforeRelaunch = false;
                if (launchAttemptCount > 0)
                {
                    manifestMissCountAfterLaunch++;
                    if (manifestMissCountAfterLaunch < ManifestPublicationGraceProbeCount)
                    {
                        shouldDelayBeforeRelaunch = true;
                    }
                    else if (launchAttemptCount >= MaxLaunchAttempts)
                    {
                        return SupervisorBootstrapResult.Failure(ExecutionError.InternalError(
                            $"{LaunchFailureMessage} Attempts={launchAttemptCount}."));
                    }
                }

                if (!shouldDelayBeforeRelaunch)
                {
                    if (!deadline.TryGetRemainingTimeout(out var launchTimeout))
                    {
                        return SupervisorBootstrapResult.Failure(ExecutionError.Timeout(
                            "Timed out before supervisor launch could begin."));
                    }

                    ExecutionError? launchError;
                    using var launchCancellationScope = TimeProviderCancellationScope.CreateLinked(
                        cancellationToken,
                        launchTimeout,
                        timeProvider);
                    try
                    {
                        launchError = await processLauncher.Launch(
                                normalizedStorageRoot,
                                launchCancellationScope.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                                                              && launchCancellationScope.HasTimedOut)
                    {
                        return SupervisorBootstrapResult.Failure(ExecutionError.Timeout(
                            $"Timed out while launching supervisor. Timeout={launchTimeout.TotalMilliseconds:0}ms."));
                    }

                    if (launchError != null)
                    {
                        return SupervisorBootstrapResult.Failure(launchError);
                    }

                    launchAttemptCount++;
                    manifestMissCountAfterLaunch = 0;
                }
            }
            else
            {
                manifestMissCountAfterLaunch = 0;
            }
            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return SupervisorBootstrapResult.Failure(ExecutionError.Timeout(
                    $"Timed out while waiting for supervisor bootstrap. Timeout={timeout.TotalMilliseconds:0}ms."));
            }

            await TimeProviderDelay.Delay(SupervisorConstants.BootstrapPollDelay, timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<ManifestAvailabilityProbe> ProbeManifestAvailability (
        string storageRoot,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var manifestReadTimeout))
        {
            return ManifestAvailabilityProbe.Failure(ExecutionError.Timeout(
                "Timed out before supervisor manifest read could begin."));
        }

        SupervisorInstanceManifest? manifest;
        try
        {
            manifest = await manifestStore.ReadOrNull(
                    storageRoot,
                    manifestReadTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            return ManifestAvailabilityProbe.Failure(ExecutionError.Timeout(exception.Message));
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            await CleanupStaleRuntimeState(storageRoot, manifest: null).ConfigureAwait(false);
            return ManifestAvailabilityProbe.ShouldLaunch();
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ManifestAvailabilityProbe.Failure(ExecutionError.InvalidArgument(
                $"Supervisor manifest path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ManifestAvailabilityProbe.Failure(ExecutionError.InternalError(
                $"Failed to read supervisor manifest. {exception.Message}"));
        }

        if (manifest == null)
        {
            return ManifestAvailabilityProbe.ShouldLaunch();
        }

        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return ManifestAvailabilityProbe.Failure(ExecutionError.Timeout(
                "Timed out before supervisor reachability probe could begin."));
        }

        var pingTimeout = remainingTimeout < SupervisorConstants.PingTimeout
            ? remainingTimeout
            : SupervisorConstants.PingTimeout;
        var probeStatus = await supervisorClient.ProbeReachability(manifest, pingTimeout, cancellationToken).ConfigureAwait(false);
        if (probeStatus == SupervisorReachabilityProbeStatus.Reachable)
        {
            return ManifestAvailabilityProbe.Ready(manifest);
        }

        if (probeStatus == SupervisorReachabilityProbeStatus.TimedOut)
        {
            return ManifestAvailabilityProbe.Pending();
        }

        await CleanupStaleRuntimeState(storageRoot, manifest).ConfigureAwait(false);
        return ManifestAvailabilityProbe.ShouldLaunch();
    }

    private async ValueTask CleanupStaleRuntimeState (
        string storageRoot,
        SupervisorInstanceManifest? manifest)
    {
        try
        {
            manifestStore.DeleteIfExists(storageRoot);
        }
        catch (Exception)
        {
            // NOTE:
            // best-effort cleanup should not block relaunch when stale manifest deletion fails.
        }

        try
        {
            var endpoint = manifest != null && IpcTransportKindCodec.TryParse(manifest.EndpointTransportKind, out var transportKind)
                ? new IpcEndpoint(transportKind, manifest.EndpointAddress)
                : endpointResolver.Resolve(storageRoot);
            if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
            {
                FileUtilities.DeleteIfExists(endpoint.Address);

                UnixSocketPathUtilities.DeleteEmptyFallbackDirectoryIfPresent(
                    endpoint.Address,
                    UcliIpcEndpointNames.SupervisorAddressPrefix);
            }
        }
        catch (Exception)
        {
            // NOTE:
            // bootstrap can continue even when stale socket cleanup fails because the new listener
            // will retry binding after process-manager restart or use a freshly recreated path.
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private readonly record struct ManifestAvailabilityProbe (
        SupervisorInstanceManifest? ReadyManifest,
        ExecutionError? Error,
        bool ShouldLaunchSupervisor)
    {
        public static ManifestAvailabilityProbe Ready (SupervisorInstanceManifest manifest)
        {
            return new ManifestAvailabilityProbe(manifest, null, false);
        }

        public static ManifestAvailabilityProbe Pending ()
        {
            return new ManifestAvailabilityProbe(null, null, false);
        }

        public static ManifestAvailabilityProbe ShouldLaunch ()
        {
            return new ManifestAvailabilityProbe(null, null, true);
        }

        public static ManifestAvailabilityProbe Failure (ExecutionError error)
        {
            return new ManifestAvailabilityProbe(null, error, false);
        }
    }
}