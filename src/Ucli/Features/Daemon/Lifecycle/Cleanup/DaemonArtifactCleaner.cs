using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Implements stale daemon artifact cleanup for one project fingerprint. </summary>
internal sealed class DaemonArtifactCleaner : IDaemonArtifactCleaner
{
    private static readonly TimeSpan SessionLockAcquireTimeout = TimeSpan.FromSeconds(1);

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonLifecycleStore daemonLifecycleStore;

    private readonly IDaemonLaunchAttemptStore launchAttemptStore;

    /// <summary> Initializes a new instance of the <see cref="DaemonArtifactCleaner" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session store dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonArtifactCleaner (
        IDaemonSessionStore daemonSessionStore,
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonLaunchAttemptStore launchAttemptStore)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.launchAttemptStore = launchAttemptStore ?? throw new ArgumentNullException(nameof(launchAttemptStore));
    }

    /// <summary> Cleans stale daemon artifacts only while the persisted session is still absent. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The deadline shared by ownership revalidation and artifact deletion admission. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The cleanup operation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionMissingAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);

        return await CleanupUnderSessionLockAsync(
                unityProject,
                static currentSession =>
                {
                    if (!currentSession.IsSuccess)
                    {
                        return SessionCleanupDecision.Failure(currentSession.Error!);
                    }

                    return currentSession.Exists
                        ? SessionCleanupDecision.Preserve()
                        : SessionCleanupDecision.Cleanup();
                },
                "Failed to acquire daemon session cleanup ownership.",
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession expectedSession,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(expectedSession);
        ArgumentNullException.ThrowIfNull(deadline);

        if (expectedSession.ProjectFingerprint != unityProject.ProjectFingerprint)
        {
            return DaemonArtifactCleanupResult.Failure(ExecutionError.InvalidArgument(
                "Expected daemon session projectFingerprint does not match the cleanup target."));
        }

        return await CleanupUnderSessionLockAsync(
                unityProject,
                currentSession =>
                {
                    if (!currentSession.IsSuccess)
                    {
                        return SessionCleanupDecision.Failure(currentSession.Error!);
                    }

                    return !currentSession.Exists
                        || MatchesGeneration(currentSession.Session!, expectedSession)
                            ? SessionCleanupDecision.Cleanup()
                            : SessionCleanupDecision.Preserve();
                },
                "Failed to acquire daemon session cleanup ownership.",
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DaemonArtifactCleanupResult> CleanupIfStoppedProcessMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonProcessTerminationTarget stoppedProcess,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(stoppedProcess.ProcessId, 0);

        return await CleanupUnderSessionLockAsync(
                unityProject,
                currentSession =>
                {
                    if (!currentSession.IsSuccess)
                    {
                        return SessionCleanupDecision.Failure(currentSession.Error!);
                    }

                    return currentSession.Session is not { } session
                        || MatchesStoppedProcess(session, stoppedProcess)
                            ? SessionCleanupDecision.Cleanup()
                            : SessionCleanupDecision.Preserve();
                },
                "Failed to acquire stopped-process artifact cleanup ownership.",
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DaemonArtifactCleanupResult> CleanupIfSessionArtifactMatchesAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionArtifactIdentity expectedArtifactIdentity,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(expectedArtifactIdentity);
        ArgumentNullException.ThrowIfNull(deadline);

        return await CleanupUnderSessionLockAsync(
                unityProject,
                currentSession =>
                {
                    if (!currentSession.IsSuccess && currentSession.ArtifactIdentity is null)
                    {
                        return SessionCleanupDecision.Failure(currentSession.Error!);
                    }

                    if (currentSession.IsSuccess && !currentSession.Exists)
                    {
                        return SessionCleanupDecision.Cleanup();
                    }

                    return Equals(currentSession.ArtifactIdentity, expectedArtifactIdentity)
                        ? SessionCleanupDecision.Cleanup()
                        : SessionCleanupDecision.Preserve();
                },
                "Failed to perform generation-scoped daemon session cleanup.",
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonArtifactCleanupResult> CleanupUnderSessionLockAsync (
        ResolvedUnityProjectContext unityProject,
        Func<DaemonSessionReadResult, SessionCleanupDecision> evaluateCurrentSession,
        string ownershipFailureMessage,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return DaemonArtifactCleanupResult.Failure(ExecutionError.Timeout(
                "Timed out before daemon artifact cleanup ownership could be acquired."));
        }

        try
        {
            var lockPath = UcliStoragePathResolver.ResolveDaemonSessionLockPath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint);
            var lockAcquireTimeout = remainingTimeout < SessionLockAcquireTimeout
                ? remainingTimeout
                : SessionLockAcquireTimeout;
            using var sessionLock = await FileExclusiveLock.AcquireAsync(
                    lockPath,
                    lockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            var sessionReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                    deadline,
                    cancellationToken,
                    "Timed out before daemon cleanup ownership could be revalidated.",
                    "Timed out while revalidating daemon cleanup ownership.",
                    operationCancellationToken => daemonSessionStore.ReadAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        operationCancellationToken))
                .ConfigureAwait(false);
            if (!sessionReadOperation.IsSuccess)
            {
                return DaemonArtifactCleanupResult.Failure(sessionReadOperation.Error!);
            }

            var currentSession = sessionReadOperation.Value!;
            var decision = evaluateCurrentSession(currentSession);
            if (decision.Error is not null)
            {
                return DaemonArtifactCleanupResult.Failure(decision.Error);
            }

            if (!decision.ShouldCleanup)
            {
                return DaemonArtifactCleanupResult.Success();
            }

            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return DaemonArtifactCleanupResult.Failure(ExecutionError.Timeout(
                    "Timed out before daemon artifact deletion could begin."));
            }

            return await CleanupCoreAsync(unityProject, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            return DaemonArtifactCleanupResult.Failure(ExecutionError.Timeout(
                $"{ownershipFailureMessage} {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DaemonArtifactCleanupResult.Failure(ExecutionError.InternalError(
                $"{ownershipFailureMessage} {exception.Message}"));
        }
    }

    private async ValueTask<DaemonArtifactCleanupResult> CleanupCoreAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        var deleteSessionResult = DeleteSessionArtifact(unityProject, cancellationToken);
        if (!deleteSessionResult.IsSuccess)
        {
            return DaemonArtifactCleanupResult.Failure(deleteSessionResult.Error!);
        }

        var deleteEndpointResult = DeleteEndpointResidue(unityProject);
        if (!deleteEndpointResult.IsSuccess)
        {
            return deleteEndpointResult;
        }

        var deleteLifecycleResult = await daemonLifecycleStore.DeleteAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!deleteLifecycleResult.IsSuccess)
        {
            return DaemonArtifactCleanupResult.Failure(deleteLifecycleResult.Error!);
        }

        var pruneResult = await launchAttemptStore.PruneAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                keepCount: 20,
                cancellationToken)
            .ConfigureAwait(false);
        if (!pruneResult.IsSuccess)
        {
            return DaemonArtifactCleanupResult.Failure(pruneResult.Error!);
        }

        return DaemonArtifactCleanupResult.Success(pruneResult.DeletedCount);
    }

    private static DaemonArtifactCleanupResult DeleteEndpointResidue (
        ResolvedUnityProjectContext unityProject)
    {
        try
        {
            var unixSocketPath = UcliIpcEndpointResolver.ResolveDaemonUnixSocketPathOrNull(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint);
            if (unixSocketPath is not null)
            {
                FileUtilities.DeleteIfExists(unixSocketPath);
            }

            return DaemonArtifactCleanupResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DaemonArtifactCleanupResult.Failure(ExecutionError.InternalError(
                $"Failed to cleanup daemon endpoint residue. {exception.Message}"));
        }
    }

    private static bool MatchesGeneration (
        DaemonSession currentSession,
        DaemonSession expectedSession)
    {
        return currentSession.SessionGenerationId == expectedSession.SessionGenerationId;
    }

    private static bool MatchesStoppedProcess (
        DaemonSession currentSession,
        DaemonProcessTerminationTarget stoppedProcess)
    {
        return currentSession.ProcessId == stoppedProcess.ProcessId
            && currentSession.ProcessStartedAtUtc == stoppedProcess.ProcessStartedAtUtc;
    }

    private static DaemonSessionStoreOperationResult DeleteSessionArtifact (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sessionPath = UcliStoragePathResolver.ResolveSessionPath(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);

        try
        {
            FileUtilities.DeleteIfExists(sessionPath);
            return DaemonSessionStoreOperationResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DaemonSessionStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to delete daemon session file: {sessionPath.Value}. {exception.Message}"));
        }
    }

    private readonly record struct SessionCleanupDecision (
        bool ShouldCleanup,
        ExecutionError? Error)
    {
        public static SessionCleanupDecision Cleanup ()
        {
            return new SessionCleanupDecision(ShouldCleanup: true, Error: null);
        }

        public static SessionCleanupDecision Preserve ()
        {
            return new SessionCleanupDecision(ShouldCleanup: false, Error: null);
        }

        public static SessionCleanupDecision Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new SessionCleanupDecision(ShouldCleanup: false, Error: error);
        }
    }
}
