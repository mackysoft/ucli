using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements safe daemon artifact cleanup workflow for one project fingerprint. </summary>
internal sealed class DaemonCleanupOperation : IDaemonCleanupOperation
{
    private const string MetadataUnavailableProbeSessionToken = "ucli-daemon-cleanup-probe";

    private readonly IProjectLifecycleLockProvider lifecycleLockProvider;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonArtifactCleaner artifactCleaner;

    private readonly IDaemonCleanupReachabilityProbe cleanupReachabilityProbe;

    private readonly IDaemonInvalidSessionCleanupSafetyEvaluator invalidSessionCleanupSafetyEvaluator;

    private readonly IDaemonProcessIdentityAssessor daemonProcessIdentityAssessor;

    /// <summary> Initializes a new instance of the <see cref="DaemonCleanupOperation" /> class. </summary>
    /// <param name="lifecycleLockProvider"> The project lifecycle lock provider dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    /// <param name="artifactCleaner"> The daemon artifact-cleaner dependency. </param>
    /// <param name="cleanupReachabilityProbe"> The cleanup reachability-probe dependency. </param>
    /// <param name="invalidSessionCleanupSafetyEvaluator"> The invalid-session cleanup safety-evaluator dependency. </param>
    /// <param name="daemonProcessIdentityAssessor"> The daemon process-identity assessor dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonCleanupOperation (
        IProjectLifecycleLockProvider lifecycleLockProvider,
        IDaemonSessionStore daemonSessionStore,
        IDaemonArtifactCleaner artifactCleaner,
        IDaemonInvalidSessionCleanupSafetyEvaluator invalidSessionCleanupSafetyEvaluator,
        IDaemonCleanupReachabilityProbe cleanupReachabilityProbe,
        IDaemonProcessIdentityAssessor daemonProcessIdentityAssessor)
    {
        this.lifecycleLockProvider = lifecycleLockProvider ?? throw new ArgumentNullException(nameof(lifecycleLockProvider));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.artifactCleaner = artifactCleaner ?? throw new ArgumentNullException(nameof(artifactCleaner));
        this.invalidSessionCleanupSafetyEvaluator = invalidSessionCleanupSafetyEvaluator ?? throw new ArgumentNullException(nameof(invalidSessionCleanupSafetyEvaluator));
        this.cleanupReachabilityProbe = cleanupReachabilityProbe ?? throw new ArgumentNullException(nameof(cleanupReachabilityProbe));
        this.daemonProcessIdentityAssessor = daemonProcessIdentityAssessor ?? throw new ArgumentNullException(nameof(daemonProcessIdentityAssessor));
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
            return await HandleReachabilityResult(
                    unityProject,
                    deadline,
                    trustedSession: null,
                    MetadataUnavailableProbeSessionToken,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await HandleReachabilityResult(
                unityProject,
                deadline,
                readResult.Session,
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
                    trustedSession: null,
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

        return await HandleProbeResult(unityProject, deadline, cleanupProbeResult, trustedSession: null, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DaemonCleanupResult> HandleReachabilityResult (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonSession? trustedSession,
        string sessionToken,
        CancellationToken cancellationToken)
    {
        var probeResult = await cleanupReachabilityProbe.Probe(
                unityProject,
                deadline,
                sessionToken,
                cancellationToken)
            .ConfigureAwait(false);

        return await HandleProbeResult(unityProject, deadline, probeResult, trustedSession, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DaemonCleanupResult> HandleProbeResult (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonCleanupReachabilityProbeResult probeResult,
        DaemonSession? trustedSession,
        CancellationToken cancellationToken)
    {
        if (CanPromoteUncertainNamedPipeConnectTimeoutToNotRunning(trustedSession, probeResult))
        {
            return await CleanupArtifactsWithinBudget(unityProject, deadline, cancellationToken).ConfigureAwait(false);
        }

        return probeResult.Status switch
        {
            DaemonCleanupReachabilityStatus.NotRunning => await CleanupArtifactsWithinBudget(unityProject, deadline, cancellationToken).ConfigureAwait(false),
            DaemonCleanupReachabilityStatus.Running => DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.Running),
            DaemonCleanupReachabilityStatus.Uncertain => DaemonCleanupResult.Skipped(DaemonCleanupSkipReason.UncertainReachability),
            DaemonCleanupReachabilityStatus.Failed => DaemonCleanupResult.Failure(probeResult.Error!),
            _ => throw new ArgumentOutOfRangeException(nameof(probeResult), probeResult.Status, "Unsupported cleanup reachability status."),
        };
    }

    private bool CanPromoteUncertainNamedPipeConnectTimeoutToNotRunning (
        DaemonSession? trustedSession,
        DaemonCleanupReachabilityProbeResult probeResult)
    {
        if (trustedSession == null
            || probeResult.Status != DaemonCleanupReachabilityStatus.Uncertain
            || probeResult.UncertainReason != DaemonCleanupReachabilityUncertainReason.ConnectTimeout)
        {
            return false;
        }

        if (!IpcTransportKindCodec.TryParse(trustedSession.EndpointTransportKind, out var transportKind)
            || transportKind != IpcTransportKind.NamedPipe
            || trustedSession.ProcessId is not int processId
            || processId <= 0
            || trustedSession.IssuedAtUtc == default)
        {
            return false;
        }

        // NOTE:
        // Named pipe connect timeout is ambiguous on its own. We only upgrade it to not-running
        // when trusted persisted session metadata still proves that the recorded daemon process
        // itself is already gone, which covers the common Windows stale-session recovery path.
        var identityAssessment = daemonProcessIdentityAssessor.AssessByProcessId(processId, trustedSession.IssuedAtUtc);
        return identityAssessment.Status == DaemonProcessIdentityAssessmentStatus.NotRunning;
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