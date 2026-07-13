using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Implements safe daemon artifact cleanup workflow for one project fingerprint. </summary>
internal sealed class DaemonCleanupOperation : IDaemonCleanupOperation
{
    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    private readonly IDaemonCleanupReachabilityProbe cleanupReachabilityProbe;

    private readonly IDaemonInvalidSessionCleanupSafetyEvaluator invalidSessionCleanupSafetyEvaluator;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonCleanupOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact-cleaner dependency. </param>
    /// <param name="invalidSessionCleanupSafetyEvaluator"> The invalid-session cleanup safety-evaluator dependency. </param>
    /// <param name="cleanupReachabilityProbe"> The cleanup reachability-probe dependency. </param>
    /// <param name="timeProvider"> The time provider used for cleanup deadline accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonCleanupOperation (
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IDaemonSessionStore daemonSessionStore,
        IDaemonArtifactCleaner artifactCleaner,
        IDaemonInvalidSessionCleanupSafetyEvaluator invalidSessionCleanupSafetyEvaluator,
        IDaemonCleanupReachabilityProbe cleanupReachabilityProbe,
        TimeProvider timeProvider)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
        this.invalidSessionCleanupSafetyEvaluator = invalidSessionCleanupSafetyEvaluator ?? throw new ArgumentNullException(nameof(invalidSessionCleanupSafetyEvaluator));
        this.cleanupReachabilityProbe = cleanupReachabilityProbe ?? throw new ArgumentNullException(nameof(cleanupReachabilityProbe));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Cleans safe daemon artifacts for the specified Unity project context. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The daemon cleanup timeout. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon cleanup result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonCleanupResult> CleanupAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!deadline.TryGetRemainingTimeout(out var lockAcquireTimeout))
        {
            return DaemonCleanupResult.Failure(ExecutionError.Timeout("Timed out before daemon cleanup workflow began."));
        }

        IAsyncDisposable lockHandle;
        try
        {
            lockHandle = await lifecycleLockProvider.AcquireAsync(
                    new ProjectLifecycleLockRequest(unityProject.UnityProjectRoot),
                    lockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return DaemonCleanupResult.Failure(ExecutionError.Timeout(
                $"Timed out while waiting for project lifecycle lock. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return DaemonCleanupResult.Failure(ExecutionError.InternalError(
                $"Failed to acquire project lifecycle lock. {exception.Message}"));
        }

        await using var acquiredLock = lockHandle;
        var readResult = await daemonSessionStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return await HandleInvalidSessionReadAsync(unityProject, readResult, deadline, cancellationToken).ConfigureAwait(false);
        }

        if (!readResult.Exists)
        {
            return await HandleReachabilityWithoutSessionTokenAsync(
                    unityProject,
                    deadline,
                    expectedSession: null,
                    expectedArtifactIdentity: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return await HandleReachabilityWithSessionTokenAsync(
                unityProject,
                deadline,
                readResult.Session!.SessionToken,
                readResult.Session,
                expectedArtifactIdentity: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonCleanupResult> HandleInvalidSessionReadAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSessionReadResult readResult,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (readResult.FailureKind != DaemonSessionReadFailureKind.InvalidSession)
        {
            return DaemonCleanupResult.Failure(readResult.Error!);
        }

        if (readResult.Session == null)
        {
            return await HandleReachabilityWithoutSessionTokenAsync(
                    unityProject,
                    deadline,
                    expectedSession: null,
                    expectedArtifactIdentity: readResult.ArtifactIdentity,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        // NOTE:
        // Parseable invalid sessions that still point to a plausible live daemon must block
        // destructive cleanup. Once that condition is met, probing must not override the
        // non-destructive skip with an unrelated failure.
        var requiresUnsafeSkip = invalidSessionCleanupSafetyEvaluator.RequiresUnsafeSkip(unityProject, readResult.Session);
        if (requiresUnsafeSkip)
        {
            return DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.UnsafeInvalidSession);
        }

        return await HandleReachabilityWithoutSessionTokenAsync(
                unityProject,
                deadline,
                readResult.Session,
                readResult.ArtifactIdentity,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonCleanupResult> HandleReachabilityWithoutSessionTokenAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonSession? expectedSession,
        DaemonSessionArtifactIdentity? expectedArtifactIdentity,
        CancellationToken cancellationToken)
    {
        var probeResult = await cleanupReachabilityProbe.ProbeWithoutSessionTokenAsync(
                unityProject,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);

        return await HandleProbeResultAsync(
                unityProject,
                deadline,
                probeResult,
                expectedSession,
                expectedArtifactIdentity,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonCleanupResult> HandleReachabilityWithSessionTokenAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        string sessionToken,
        DaemonSession? expectedSession,
        DaemonSessionArtifactIdentity? expectedArtifactIdentity,
        CancellationToken cancellationToken)
    {
        var probeResult = await cleanupReachabilityProbe.ProbeWithSessionTokenAsync(
                unityProject,
                deadline,
                sessionToken,
                cancellationToken)
            .ConfigureAwait(false);

        return await HandleProbeResultAsync(
                unityProject,
                deadline,
                probeResult,
                expectedSession,
                expectedArtifactIdentity,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<DaemonCleanupResult> HandleProbeResultAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonCleanupReachabilityProbeResult probeResult,
        DaemonSession? expectedSession,
        DaemonSessionArtifactIdentity? expectedArtifactIdentity,
        CancellationToken cancellationToken)
    {
        return probeResult.Status switch
        {
            DaemonCleanupReachabilityStatus.NotRunning => await CleanupArtifactsWithinBudgetAsync(
                    unityProject,
                    deadline,
                    expectedSession,
                    expectedArtifactIdentity,
                    cancellationToken)
                .ConfigureAwait(false),
            DaemonCleanupReachabilityStatus.Running => DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.Running),
            DaemonCleanupReachabilityStatus.Uncertain => DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.UncertainReachability),
            DaemonCleanupReachabilityStatus.Failed => DaemonCleanupResult.Failure(probeResult.Error!),
            _ => throw new ArgumentOutOfRangeException(nameof(probeResult), probeResult.Status, "Unsupported cleanup reachability status."),
        };
    }

    private async ValueTask<DaemonCleanupResult> CleanupArtifactsWithinBudgetAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonSession? expectedSession,
        DaemonSessionArtifactIdentity? expectedArtifactIdentity,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return DaemonCleanupResult.Failure(ExecutionError.Timeout(
                "Timed out before daemon artifact cleanup could begin."));
        }

        DaemonArtifactCleanupResult cleanupResult;
        if (expectedArtifactIdentity is not null)
        {
            cleanupResult = await artifactCleaner.CleanupIfSessionArtifactMatchesAsync(
                    unityProject,
                    expectedArtifactIdentity,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (expectedSession is not null)
        {
            cleanupResult = await artifactCleaner.CleanupIfSessionMatchesAsync(
                    unityProject,
                    expectedSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            cleanupResult = await artifactCleaner.CleanupIfSessionMissingAsync(unityProject, cancellationToken).ConfigureAwait(false);
        }

        return cleanupResult.IsSuccess
            ? DaemonCleanupResult.Completed(cleanupResult.DeletedLaunchAttemptCount)
            : DaemonCleanupResult.Failure(cleanupResult.Error!);
    }
}
