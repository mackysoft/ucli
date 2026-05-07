using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Implements safe daemon artifact cleanup workflow for one project fingerprint. </summary>
internal sealed class DaemonCleanupOperation : IDaemonCleanupOperation
{
    private const string MetadataUnavailableProbeSessionToken = "ucli-daemon-cleanup-probe";

    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    private readonly IDaemonCleanupReachabilityProbe cleanupReachabilityProbe;

    private readonly IDaemonInvalidSessionCleanupSafetyEvaluator invalidSessionCleanupSafetyEvaluator;

    /// <summary> Initializes a new instance of the <see cref="DaemonCleanupOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact-cleaner dependency. </param>
    /// <param name="cleanupReachabilityProbe"> The cleanup reachability-probe dependency. </param>
    /// <param name="invalidSessionCleanupSafetyEvaluator"> The invalid-session cleanup safety-evaluator dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonCleanupOperation (
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IDaemonSessionStore daemonSessionStore,
        IDaemonArtifactCleaner artifactCleaner,
        IDaemonInvalidSessionCleanupSafetyEvaluator invalidSessionCleanupSafetyEvaluator,
        IDaemonCleanupReachabilityProbe cleanupReachabilityProbe)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
        this.invalidSessionCleanupSafetyEvaluator = invalidSessionCleanupSafetyEvaluator ?? throw new ArgumentNullException(nameof(invalidSessionCleanupSafetyEvaluator));
        this.cleanupReachabilityProbe = cleanupReachabilityProbe ?? throw new ArgumentNullException(nameof(cleanupReachabilityProbe));
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
                    unityProject,
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
            return await HandleReachabilityResult(
                    unityProject,
                    deadline,
                    MetadataUnavailableProbeSessionToken,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await HandleReachabilityResult(
                unityProject,
                deadline,
                readResult.Session!.SessionToken,
                cancellationToken)
            .ConfigureAwait(false);
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

        if (readResult.Session == null)
        {
            return await HandleReachabilityResult(
                    unityProject,
                    deadline,
                    MetadataUnavailableProbeSessionToken,
                    cancellationToken)
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

        var cleanupProbeResult = await cleanupReachabilityProbe.Probe(
                unityProject,
                deadline,
                MetadataUnavailableProbeSessionToken,
                cancellationToken)
            .ConfigureAwait(false);
        if (cleanupProbeResult.Status == DaemonCleanupReachabilityStatus.Failed)
        {
            return DaemonCleanupResult.Failure(cleanupProbeResult.Error!);
        }

        return await HandleProbeResult(unityProject, deadline, cleanupProbeResult, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DaemonCleanupResult> HandleReachabilityResult (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        var probeResult = await cleanupReachabilityProbe.Probe(
                unityProject,
                deadline,
                sessionToken,
                cancellationToken)
            .ConfigureAwait(false);

        return await HandleProbeResult(unityProject, deadline, probeResult, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DaemonCleanupResult> HandleProbeResult (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonCleanupReachabilityProbeResult probeResult,
        CancellationToken cancellationToken)
    {
        return probeResult.Status switch
        {
            DaemonCleanupReachabilityStatus.NotRunning => await CleanupArtifactsWithinBudget(unityProject, deadline, cancellationToken).ConfigureAwait(false),
            DaemonCleanupReachabilityStatus.Running => DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.Running),
            DaemonCleanupReachabilityStatus.Uncertain => DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.UncertainReachability),
            DaemonCleanupReachabilityStatus.Failed => DaemonCleanupResult.Failure(probeResult.Error!),
            _ => throw new ArgumentOutOfRangeException(nameof(probeResult), probeResult.Status, "Unsupported cleanup reachability status."),
        };
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
