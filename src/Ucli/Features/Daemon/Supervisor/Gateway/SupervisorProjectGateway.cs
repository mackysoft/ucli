using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Gateway;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;

/// <summary> Encapsulates supervisor bootstrap, reachability, and project control flows for the daemon lifecycle port. </summary>
internal sealed class SupervisorProjectGateway : IDaemonProjectLifecycleGateway
{
    private readonly SupervisorBootstrapper supervisorBootstrapper;

    private readonly SupervisorManifestStore supervisorManifestStore;

    private readonly SupervisorClient supervisorClient;

    private readonly SupervisorBootstrapLockProvider bootstrapLockProvider;

    private readonly SupervisorEndpointResolver endpointResolver;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="SupervisorProjectGateway" /> class. </summary>
    public SupervisorProjectGateway (
        SupervisorBootstrapper supervisorBootstrapper,
        SupervisorManifestStore supervisorManifestStore,
        SupervisorClient supervisorClient,
        SupervisorBootstrapLockProvider bootstrapLockProvider,
        SupervisorEndpointResolver endpointResolver,
        TimeProvider timeProvider)
    {
        this.supervisorBootstrapper = supervisorBootstrapper ?? throw new ArgumentNullException(nameof(supervisorBootstrapper));
        this.supervisorManifestStore = supervisorManifestStore ?? throw new ArgumentNullException(nameof(supervisorManifestStore));
        this.supervisorClient = supervisorClient ?? throw new ArgumentNullException(nameof(supervisorClient));
        this.bootstrapLockProvider = bootstrapLockProvider ?? throw new ArgumentNullException(nameof(bootstrapLockProvider));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async ValueTask<DaemonStartResult> EnsureRunningAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        IDaemonProjectLifecycleProgressObserver? progressObserver = null,
        ICommandProgressSink? supervisorProgressSink = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var timeoutBudget = ExecutionTimeoutBudget.Start(timeout, timeProvider);
        await EmitProgressOutsideBudgetAsync(
                timeoutBudget,
                progressObserver is null
                    ? null
                    : token => progressObserver.EmitSupervisorBootstrapStartedAsync(token),
                cancellationToken)
            .ConfigureAwait(false);
        if (!timeoutBudget.TryGetRemainingTimeout(out var bootstrapTimeout))
        {
            var failure = DaemonStartResult.Failure(ExecutionError.Timeout(
                "Timed out before supervisor bootstrap could begin."));
            await EmitProgressOutsideBudgetAsync(
                    timeoutBudget,
                    progressObserver is null
                        ? null
                        : token => progressObserver.EmitSupervisorBootstrapCompletedAsync(failure.Error, token),
                    cancellationToken)
                .ConfigureAwait(false);
            return failure;
        }

        var bootstrapResult = await supervisorBootstrapper.EnsureReadyAsync(
                unityProject.RepositoryRoot,
                bootstrapTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        await EmitProgressOutsideBudgetAsync(
                timeoutBudget,
                progressObserver is null
                    ? null
                    : token => progressObserver.EmitSupervisorBootstrapCompletedAsync(bootstrapResult.Error, token),
                cancellationToken)
            .ConfigureAwait(false);
        if (!bootstrapResult.IsSuccess)
        {
            return DaemonStartResult.Failure(bootstrapResult.Error!);
        }

        await EmitProgressOutsideBudgetAsync(
                timeoutBudget,
                progressObserver is null
                    ? null
                    : token => progressObserver.EmitEnsureRunningStartedAsync(token),
                cancellationToken)
            .ConfigureAwait(false);
        if (!timeoutBudget.TryGetRemainingTimeout(out var ensureRunningTimeout))
        {
            var failure = DaemonStartResult.Failure(ExecutionError.Timeout(
                "Timed out before supervisor ensureRunning could begin."));
            await EmitProgressOutsideBudgetAsync(
                    timeoutBudget,
                    progressObserver is null
                        ? null
                        : token => progressObserver.EmitEnsureRunningCompletedAsync(failure, token),
                    cancellationToken)
                .ConfigureAwait(false);
            return failure;
        }

        var ensureRunningRequestId = Guid.NewGuid();
        var ensureRunningDeadlineUtc = timeProvider.GetUtcNow().Add(ensureRunningTimeout);
        var manifest = bootstrapResult.Manifest!;
        var startResult = await supervisorClient.EnsureRunningAsync(
                manifest,
                ensureRunningRequestId,
                unityProject,
                ensureRunningDeadlineUtc,
                ensureRunningTimeout,
                editorMode,
                onStartupBlocked,
                supervisorProgressSink,
                cancellationToken)
            .ConfigureAwait(false);
        if (IsSessionTokenInvalid(startResult.Error))
        {
            if (!timeoutBudget.TryGetRemainingTimeout(out var manifestReloadTimeout))
            {
                startResult = DaemonStartResult.Failure(ExecutionError.Timeout(
                    "Timed out before reloading the successor supervisor manifest."));
            }
            else
            {
                var reloadResult = await ReloadSuccessorManifestAsync(
                        unityProject.RepositoryRoot,
                        manifest.SessionToken,
                        manifestReloadTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (reloadResult.Error != null)
                {
                    startResult = DaemonStartResult.Failure(reloadResult.Error);
                }
                else if (reloadResult.Manifest != null)
                {
                    if (!timeoutBudget.TryGetRemainingTimeout(out var replayTimeout))
                    {
                        startResult = DaemonStartResult.Failure(ExecutionError.Timeout(
                            "Timed out before replaying ensureRunning against the successor supervisor."));
                    }
                    else
                    {
                        startResult = await supervisorClient.EnsureRunningAsync(
                                reloadResult.Manifest,
                                ensureRunningRequestId,
                                unityProject,
                                ensureRunningDeadlineUtc,
                                replayTimeout,
                                editorMode,
                                onStartupBlocked,
                                supervisorProgressSink,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
        }

        await EmitProgressOutsideBudgetAsync(
                timeoutBudget,
                progressObserver is null
                    ? null
                    : token => progressObserver.EmitEnsureRunningCompletedAsync(startResult, token),
                cancellationToken)
            .ConfigureAwait(false);
        return startResult;
    }

    /// <inheritdoc />
    public async ValueTask<DaemonStopResult?> TryStopProjectAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var canRetryMalformedSuccessorManifest = true;
        var canReloadAfterTokenRejection = true;
        while (true)
        {
            if (!deadline.TryGetRemainingTimeout(out var manifestReadTimeout))
            {
                return DaemonStopResult.Failure(ExecutionError.Timeout(
                    "Timed out before supervisor manifest read could begin."));
            }

            SupervisorInstanceManifest? manifest;
            try
            {
                manifest = await supervisorManifestStore.ReadOrNullAsync(
                        unityProject.RepositoryRoot,
                        manifestReadTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                return DaemonStopResult.Failure(ExecutionError.Timeout(exception.Message));
            }
            catch (SupervisorManifestFormatException exception)
            {
                var cleanupResult = await TryCleanupMalformedSupervisorRuntimeAsync(
                        unityProject.RepositoryRoot,
                        exception.ArtifactIdentity,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (cleanupResult.Error != null)
                {
                    return DaemonStopResult.Failure(cleanupResult.Error);
                }

                if (canRetryMalformedSuccessorManifest
                    && cleanupResult.Status == SupervisorManifestCleanupStatus.GenerationMismatch)
                {
                    canRetryMalformedSuccessorManifest = false;
                    continue;
                }

                return null;
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                return DaemonStopResult.Failure(ExecutionError.InternalError(
                    $"Failed to identify malformed supervisor manifest generation. {exception.Message}"));
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

            SupervisorReachabilityProbeStatus probeStatus;
            while (true)
            {
                if (!deadline.TryGetRemainingTimeout(out var probeBudget))
                {
                    return DaemonStopResult.Failure(ExecutionError.Timeout(
                        "Timed out before supervisor stop probe could begin."));
                }

                var probeTimeout = probeBudget < SupervisorConstants.PingTimeout
                    ? probeBudget
                    : SupervisorConstants.PingTimeout;
                probeStatus = await supervisorClient.ProbeReachabilityAsync(
                        manifest,
                        probeTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (probeStatus != SupervisorReachabilityProbeStatus.SessionTokenRejected)
                {
                    break;
                }

                if (!canReloadAfterTokenRejection
                    || !deadline.TryGetRemainingTimeout(out var probeManifestReloadTimeout))
                {
                    return null;
                }

                canReloadAfterTokenRejection = false;
                var probeReloadResult = await ReloadSuccessorManifestAsync(
                        unityProject.RepositoryRoot,
                        manifest.SessionToken,
                        probeManifestReloadTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (probeReloadResult.Error != null)
                {
                    return DaemonStopResult.Failure(probeReloadResult.Error);
                }

                if (probeReloadResult.Manifest == null)
                {
                    return null;
                }

                manifest = probeReloadResult.Manifest;
            }

            if (probeStatus == SupervisorReachabilityProbeStatus.Unreachable)
            {
                return null;
            }

            if (!deadline.TryGetRemainingTimeout(out var stopTimeout))
            {
                return DaemonStopResult.Failure(ExecutionError.Timeout(
                    "Timed out before supervisor stopProject could begin."));
            }

            var stopProjectRequestId = Guid.NewGuid();
            var stopProjectDeadlineUtc = timeProvider.GetUtcNow().Add(stopTimeout);
            var stopResult = await supervisorClient.StopProjectAsync(
                    manifest,
                    stopProjectRequestId,
                    unityProject,
                    stopProjectDeadlineUtc,
                    stopTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!IsSessionTokenInvalid(stopResult.Error))
            {
                return stopResult;
            }

            if (!canReloadAfterTokenRejection)
            {
                return stopResult;
            }

            if (!deadline.TryGetRemainingTimeout(out var manifestReloadTimeout))
            {
                return DaemonStopResult.Failure(ExecutionError.Timeout(
                    "Timed out before reloading the successor supervisor manifest."));
            }

            canReloadAfterTokenRejection = false;
            var reloadResult = await ReloadSuccessorManifestAsync(
                    unityProject.RepositoryRoot,
                    manifest.SessionToken,
                    manifestReloadTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (reloadResult.Error != null)
            {
                return DaemonStopResult.Failure(reloadResult.Error);
            }

            if (reloadResult.Manifest == null)
            {
                return stopResult;
            }

            if (!deadline.TryGetRemainingTimeout(out var replayTimeout))
            {
                return DaemonStopResult.Failure(ExecutionError.Timeout(
                    "Timed out before replaying stopProject against the successor supervisor."));
            }

            return await supervisorClient.StopProjectAsync(
                    reloadResult.Manifest,
                    stopProjectRequestId,
                    unityProject,
                    stopProjectDeadlineUtc,
                    replayTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<SupervisorManifestReloadResult> ReloadSuccessorManifestAsync (
        string repositoryRoot,
        IpcSessionToken rejectedSessionToken,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await supervisorManifestStore.ReadAfterEndpointPublicationAsync(
                    repositoryRoot,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (manifest == null
                || manifest.SessionToken.Equals(rejectedSessionToken))
            {
                return new SupervisorManifestReloadResult(null, null);
            }

            return new SupervisorManifestReloadResult(manifest, null);
        }
        catch (TimeoutException exception)
        {
            return new SupervisorManifestReloadResult(
                null,
                ExecutionError.Timeout(exception.Message));
        }
        catch (SupervisorManifestFormatException exception)
        {
            return new SupervisorManifestReloadResult(
                null,
                ExecutionError.InternalError(
                    $"Successor supervisor manifest is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return new SupervisorManifestReloadResult(
                null,
                ExecutionError.InvalidArgument(
                    $"Successor supervisor manifest path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new SupervisorManifestReloadResult(
                null,
                ExecutionError.InternalError(
                    $"Successor supervisor manifest could not be read. {exception.Message}"));
        }
    }

    private async ValueTask<SupervisorRuntimeCleanupResult> TryCleanupMalformedSupervisorRuntimeAsync (
        string repositoryRoot,
        Sha256Digest expectedArtifactIdentity,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var lockTimeout))
        {
            return new SupervisorRuntimeCleanupResult(
                null,
                ExecutionError.Timeout(
                    "Timed out before malformed supervisor runtime cleanup could begin."));
        }

        try
        {
            await using var bootstrapLock = await bootstrapLockProvider.AcquireAsync(
                    repositoryRoot,
                    lockTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!deadline.TryGetRemainingTimeout(out var runtimeCleanupTimeout))
            {
                return new SupervisorRuntimeCleanupResult(
                    null,
                    ExecutionError.Timeout(
                        "Timed out before malformed supervisor manifest cleanup could begin."));
            }

            var cleanupStatus = await supervisorManifestStore.CleanupObservedRuntimeIfMalformedArtifactMatchesAsync(
                    repositoryRoot,
                    expectedArtifactIdentity,
                    endpointResolver.ResolveCanonicalEndpoint(repositoryRoot),
                    runtimeCleanupTimeout < SupervisorConstants.RuntimeOwnershipLockTimeout
                        ? runtimeCleanupTimeout
                        : SupervisorConstants.RuntimeOwnershipLockTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return new SupervisorRuntimeCleanupResult(cleanupStatus, null);
        }
        catch (TimeoutException exception)
        {
            return new SupervisorRuntimeCleanupResult(
                null,
                ExecutionError.Timeout(
                    $"Timed out while waiting to clean malformed supervisor runtime state. {exception.Message}"));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return new SupervisorRuntimeCleanupResult(
                null,
                ExecutionError.InvalidArgument(
                    $"Supervisor manifest cleanup path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // NOTE:
            // malformed supervisor metadata should not block the direct-stop fallback when only
            // the best-effort manifest cleanup failed.
            return new SupervisorRuntimeCleanupResult(null, null);
        }
    }

    private readonly record struct SupervisorRuntimeCleanupResult (
        SupervisorManifestCleanupStatus? Status,
        ExecutionError? Error);

    private readonly record struct SupervisorManifestReloadResult (
        SupervisorInstanceManifest? Manifest,
        ExecutionError? Error);

    private static bool IsSessionTokenInvalid (ExecutionError? error)
    {
        return error?.Code == IpcSessionErrorCodes.SessionTokenInvalid;
    }

    private static async ValueTask EmitProgressOutsideBudgetAsync (
        ExecutionTimeoutBudget timeoutBudget,
        Func<CancellationToken, ValueTask>? emit,
        CancellationToken cancellationToken)
    {
        if (emit is null)
        {
            return;
        }

        using var excludedSection = timeoutBudget.BeginExcludedSection();
        await emit(cancellationToken).ConfigureAwait(false);
    }
}
