using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements safe daemon artifact cleanup workflow for one project fingerprint. </summary>
internal sealed class DaemonCleanupOperation : IDaemonCleanupOperation
{
    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonPingClient daemonPingClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    private readonly IDaemonInvalidSessionCleanupSafetyEvaluator invalidSessionCleanupSafetyEvaluator;

    /// <summary> Initializes a new instance of the <see cref="DaemonCleanupOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    /// <param name="daemonPingClient"> The daemon ping-client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability-classifier dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact-cleaner dependency. </param>
    /// <param name="invalidSessionCleanupSafetyEvaluator"> The invalid-session cleanup safety-evaluator dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonCleanupOperation (
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IDaemonSessionStore daemonSessionStore,
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonArtifactCleaner artifactCleaner,
        IDaemonInvalidSessionCleanupSafetyEvaluator invalidSessionCleanupSafetyEvaluator)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
        this.invalidSessionCleanupSafetyEvaluator = invalidSessionCleanupSafetyEvaluator ?? throw new ArgumentNullException(nameof(invalidSessionCleanupSafetyEvaluator));
    }

    /// <summary> Cleans safe daemon artifacts for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon cleanup timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon cleanup result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonCleanupResult> Cleanup (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout);
        if (!deadline.TryGetRemainingTimeout(out var lockAcquireTimeout))
        {
            return DaemonCleanupResult.Failure(ExecutionError.Timeout("Timed out before daemon cleanup workflow began."));
        }

        IAsyncDisposable lockHandle;
        try
        {
            lockHandle = await lifecycleLockProvider.Acquire(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    lockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            return DaemonCleanupResult.Failure(ExecutionError.Timeout(
                $"Timed out while waiting for project lifecycle lock. {exception.Message}"));
        }

        await using var acquiredLock = lockHandle;
        var readResult = await daemonSessionStore.Read(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return await HandleInvalidSessionRead(unityProject, readResult, deadline, cancellationToken).ConfigureAwait(false);
        }

        if (!readResult.Exists)
        {
            return DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.UncertainReachability);
        }

        return await HandleExistingSession(unityProject, readResult.Session!, deadline, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DaemonCleanupResult> HandleInvalidSessionRead (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (readResult.FailureKind != DaemonSessionReadFailureKind.InvalidSession)
        {
            return DaemonCleanupResult.Failure(readResult.Error!);
        }

        return invalidSessionCleanupSafetyEvaluator.CanCleanup(unityProject, readResult.Session)
            ? await CleanupArtifactsWithinBudget(unityProject, deadline, cancellationToken).ConfigureAwait(false)
            : DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.UnsafeInvalidSession);
    }

    private async ValueTask<DaemonCleanupResult> HandleExistingSession (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var pingTimeout))
        {
            return DaemonCleanupResult.Failure(ExecutionError.Timeout(
                "Timed out before daemon cleanup reachability probe could begin."));
        }

        try
        {
            await daemonPingClient.Ping(
                    unityProject,
                    pingTimeout,
                    session.SessionToken,
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.Running);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.UncertainReachability);
        }
        catch (DaemonPingResponseException exception) when (string.Equals(exception.ErrorCode, IpcErrorCodes.SessionTokenInvalid, StringComparison.Ordinal))
        {
            // NOTE:
            // Token mismatch means cleanup cannot safely prove residue while keeping the shared
            // running/not-running semantics unchanged for other reachability callers.
            return DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.UncertainReachability);
        }
        catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
        {
            return await CleanupArtifactsWithinBudget(unityProject, deadline, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.UncertainReachability);
        }
    }

    private async ValueTask<DaemonCleanupResult> CleanupArtifactsWithinBudget (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return DaemonCleanupResult.Failure(ExecutionError.Timeout(
                "Timed out before daemon artifact cleanup could begin."));
        }

        var cleanupResult = await artifactCleaner.Cleanup(unityProject, cancellationToken).ConfigureAwait(false);
        return cleanupResult.IsSuccess
            ? DaemonCleanupResult.Completed()
            : DaemonCleanupResult.Failure(cleanupResult.Error!);
    }
}