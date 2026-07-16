using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;

/// <summary> Ensures the worktree-local supervisor is running and reachable before daemon commands proceed. </summary>
internal sealed class SupervisorBootstrapper
{
    private const int MaxLaunchAttempts = 2;

    private const string LaunchFailureMessage =
        "Supervisor launch did not publish a reachable manifest before startup completed.";

    private readonly SupervisorManifestStore manifestStore;

    private readonly SupervisorClient supervisorClient;

    private readonly ISupervisorProcessManager processManager;

    private readonly SupervisorBootstrapLockProvider bootstrapLockProvider;

    private readonly SupervisorEndpointResolver endpointResolver;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="SupervisorBootstrapper" /> class. </summary>
    /// <param name="manifestStore"> The supervisor manifest-store dependency. </param>
    /// <param name="supervisorClient"> The supervisor client dependency. </param>
    /// <param name="processManager"> The supervisor process-manager dependency. </param>
    /// <param name="bootstrapLockProvider"> The bootstrap-lock provider dependency. </param>
    /// <param name="endpointResolver"> The supervisor endpoint resolver dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    public SupervisorBootstrapper (
        SupervisorManifestStore manifestStore,
        SupervisorClient supervisorClient,
        ISupervisorProcessManager processManager,
        SupervisorBootstrapLockProvider bootstrapLockProvider,
        SupervisorEndpointResolver endpointResolver,
        TimeProvider timeProvider)
    {
        this.manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        this.supervisorClient = supervisorClient ?? throw new ArgumentNullException(nameof(supervisorClient));
        this.processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        this.bootstrapLockProvider = bootstrapLockProvider ?? throw new ArgumentNullException(nameof(bootstrapLockProvider));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Ensures the supervisor for the specified storage root is running and reachable. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="timeout"> The bootstrap timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The bootstrap result. </returns>
    public async ValueTask<SupervisorBootstrapResult> EnsureReadyAsync (
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
        string normalizedStorageRoot;
        try
        {
            normalizedStorageRoot = UcliStoragePathResolver.NormalizeStorageRootPath(storageRoot);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception) || exception is ArgumentException)
        {
            return SupervisorBootstrapResult.Failure(ExecutionError.InvalidArgument(
                $"Supervisor bootstrap path is invalid. {exception.Message}"));
        }

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);

        if (!deadline.TryGetRemainingTimeout(out var lockAcquireTimeout))
        {
            return SupervisorBootstrapResult.Failure(ExecutionError.Timeout(
                "Timed out before supervisor bootstrap could begin."));
        }

        IAsyncDisposable lockHandle;
        try
        {
            lockHandle = await bootstrapLockProvider.AcquireAsync(
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
        long? latestLaunchTimestamp = null;
        ISupervisorProcessLaunchLease? pendingLaunchLease = null;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isWithinLaunchGrace = latestLaunchTimestamp is long launchTimestamp
                    && IsWithinManifestPublicationGrace(launchTimestamp);
                var manifestProbe = await ProbeManifestAvailabilityAsync(
                        normalizedStorageRoot,
                        deadline,
                        isWithinLaunchGrace,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (manifestProbe.ReadyManifest != null)
                {
                    var committedLaunchLease = pendingLaunchLease;
                    pendingLaunchLease = null;
                    if (committedLaunchLease is not null)
                    {
                        await CommitLaunchBestEffortAsync(committedLaunchLease).ConfigureAwait(false);
                    }

                    return SupervisorBootstrapResult.Success(manifestProbe.ReadyManifest);
                }

                if (manifestProbe.Error != null)
                {
                    return SupervisorBootstrapResult.Failure(manifestProbe.Error);
                }

                if (manifestProbe.ShouldLaunchSupervisor)
                {
                    // NOTE:
                    // launchd/systemd/dotnet startup can take several seconds before the supervisor
                    // writes manifest.json. During that window, relaunching or deleting the socket can
                    // destroy a healthy startup path before it becomes observable to the bootstrapper.
                    if (launchAttemptCount > 0 && isWithinLaunchGrace)
                    {
                        if (!await TryDelayBeforeNextProbeAsync(deadline, cancellationToken).ConfigureAwait(false))
                        {
                            return SupervisorBootstrapResult.Failure(ExecutionError.Timeout(
                                $"Timed out while waiting for supervisor bootstrap. Timeout={timeout.TotalMilliseconds:0}ms."));
                        }

                        continue;
                    }

                    if (pendingLaunchLease is not null)
                    {
                        var rollbackError = await TryRollbackLaunchAsync(pendingLaunchLease).ConfigureAwait(false);
                        if (rollbackError is not null)
                        {
                            return SupervisorBootstrapResult.Failure(rollbackError);
                        }

                        pendingLaunchLease = null;
                    }

                    if (launchAttemptCount >= MaxLaunchAttempts)
                    {
                        return SupervisorBootstrapResult.Failure(ExecutionError.InternalError(
                            $"{LaunchFailureMessage} Attempts={launchAttemptCount}."));
                    }

                    if (!deadline.TryGetRemainingTimeout(out var launchTimeout))
                    {
                        return SupervisorBootstrapResult.Failure(ExecutionError.Timeout(
                            "Timed out before supervisor launch could begin."));
                    }

                    SupervisorProcessLaunchResult launchResult;
                    using var launchCancellationScope = TimeProviderCancellationScope.CreateLinked(
                        cancellationToken,
                        launchTimeout,
                        timeProvider);
                    try
                    {
                        launchResult = await processManager.LaunchAsync(
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

                    pendingLaunchLease = launchResult.Lease;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (launchCancellationScope.HasTimedOut)
                    {
                        return SupervisorBootstrapResult.Failure(ExecutionError.Timeout(
                            $"Timed out while launching supervisor. Timeout={launchTimeout.TotalMilliseconds:0}ms."));
                    }

                    if (!launchResult.IsSuccess)
                    {
                        return SupervisorBootstrapResult.Failure(launchResult.Error!);
                    }

                    launchAttemptCount++;
                    latestLaunchTimestamp = timeProvider.GetTimestamp();
                }

                if (!await TryDelayBeforeNextProbeAsync(deadline, cancellationToken).ConfigureAwait(false))
                {
                    return SupervisorBootstrapResult.Failure(ExecutionError.Timeout(
                        $"Timed out while waiting for supervisor bootstrap. Timeout={timeout.TotalMilliseconds:0}ms."));
                }
            }
        }
        finally
        {
            if (pendingLaunchLease is not null)
            {
                _ = await TryRollbackLaunchAsync(pendingLaunchLease).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask CommitLaunchBestEffortAsync (ISupervisorProcessLaunchLease launchLease)
    {
        ArgumentNullException.ThrowIfNull(launchLease);

        try
        {
            await launchLease.CommitAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Readiness has already transferred process ownership; local handle cleanup must not roll it back.
        }
    }

    private static async ValueTask<ExecutionError?> TryRollbackLaunchAsync (ISupervisorProcessLaunchLease launchLease)
    {
        ArgumentNullException.ThrowIfNull(launchLease);

        try
        {
            return await launchLease.RollbackAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return ExecutionError.InternalError(
                $"Failed to roll back supervisor process launch. {exception.Message}");
        }
    }

    private async ValueTask<ManifestAvailabilityProbe> ProbeManifestAvailabilityAsync (
        string storageRoot,
        ExecutionDeadline deadline,
        bool preserveUnreachableManifest,
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
            manifest = await manifestStore.ReadOrNullAsync(
                    storageRoot,
                    manifestReadTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            return ManifestAvailabilityProbe.Failure(ExecutionError.Timeout(exception.Message));
        }
        catch (SupervisorManifestFormatException exception)
        {
            return await CleanupMalformedRuntimeStateAsync(
                    storageRoot,
                    exception.ArtifactIdentity,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return ManifestAvailabilityProbe.Failure(ExecutionError.InternalError(
                $"Failed to identify malformed supervisor manifest generation. {exception.Message}"));
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
        var probeStatus = await supervisorClient.ProbeReachabilityAsync(manifest, pingTimeout, cancellationToken).ConfigureAwait(false);
        if (probeStatus == SupervisorReachabilityProbeStatus.Reachable)
        {
            return ManifestAvailabilityProbe.Ready(manifest);
        }

        if (probeStatus == SupervisorReachabilityProbeStatus.TimedOut)
        {
            return ManifestAvailabilityProbe.Pending();
        }

        if (preserveUnreachableManifest)
        {
            return ManifestAvailabilityProbe.Pending();
        }

        return await CleanupObservedRuntimeStateAsync(storageRoot, manifest, deadline, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ManifestAvailabilityProbe> CleanupObservedRuntimeStateAsync (
        string storageRoot,
        SupervisorInstanceManifest expectedManifest,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return ManifestAvailabilityProbe.Failure(ExecutionError.Timeout(
                "Timed out before stale supervisor runtime cleanup could begin."));
        }

        try
        {
            var cleanupStatus = await manifestStore.CleanupObservedRuntimeIfManifestMatchesAsync(
                    storageRoot,
                    expectedManifest,
                    endpointResolver.ResolveUnixSocketCleanupTargetOrNull(storageRoot),
                    GetObservedRuntimeCleanupTimeout(remainingTimeout),
                    cancellationToken)
                .ConfigureAwait(false);
            return ToManifestAvailabilityProbe(cleanupStatus);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ManifestAvailabilityProbe.Failure(ExecutionError.InvalidArgument(
                $"Supervisor runtime cleanup path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return ManifestAvailabilityProbe.Pending();
        }
    }

    private async ValueTask<ManifestAvailabilityProbe> CleanupMalformedRuntimeStateAsync (
        string storageRoot,
        Sha256Digest expectedArtifactIdentity,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return ManifestAvailabilityProbe.Failure(ExecutionError.Timeout(
                "Timed out before malformed supervisor runtime cleanup could begin."));
        }

        try
        {
            var cleanupStatus = await manifestStore.CleanupObservedRuntimeIfMalformedArtifactMatchesAsync(
                    storageRoot,
                    expectedArtifactIdentity,
                    endpointResolver.ResolveUnixSocketCleanupTargetOrNull(storageRoot),
                    GetObservedRuntimeCleanupTimeout(remainingTimeout),
                    cancellationToken)
                .ConfigureAwait(false);
            return ToManifestAvailabilityProbe(cleanupStatus);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ManifestAvailabilityProbe.Failure(ExecutionError.InvalidArgument(
                $"Supervisor runtime cleanup path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return ManifestAvailabilityProbe.Pending();
        }
    }

    private static ManifestAvailabilityProbe ToManifestAvailabilityProbe (
        SupervisorManifestCleanupStatus cleanupStatus)
    {
        return cleanupStatus == SupervisorManifestCleanupStatus.GenerationMismatch
            ? ManifestAvailabilityProbe.Pending()
            : ManifestAvailabilityProbe.ShouldLaunch();
    }

    private static TimeSpan GetObservedRuntimeCleanupTimeout (TimeSpan remainingTimeout)
    {
        return remainingTimeout < SupervisorConstants.RuntimeOwnershipLockTimeout
            ? remainingTimeout
            : SupervisorConstants.RuntimeOwnershipLockTimeout;
    }

    private bool IsWithinManifestPublicationGrace (long launchTimestamp)
    {
        return timeProvider.GetElapsedTime(launchTimestamp) < SupervisorConstants.ManifestPublicationTimeout;
    }

    private async ValueTask<bool> TryDelayBeforeNextProbeAsync (
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return false;
        }

        var delay = remainingTimeout < SupervisorConstants.BootstrapPollDelay
            ? remainingTimeout
            : SupervisorConstants.BootstrapPollDelay;
        if (delay <= TimeSpan.Zero)
        {
            return false;
        }

        await TimeProviderDelay.DelayAsync(delay, timeProvider, cancellationToken).ConfigureAwait(false);
        return true;
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
