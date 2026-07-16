using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Git;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;

/// <summary> Implements daemon registration enumeration across Git worktrees. </summary>
internal sealed class DaemonListQueryService : IDaemonListQueryService
{
    private readonly IGitWorktreeQueryService gitWorktreeQueryService;

    private readonly IUnityProjectResolver unityProjectResolver;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonDiagnosisStore daemonDiagnosisStore;

    private readonly DaemonSessionProbe daemonSessionProbe;

    private readonly IDaemonReachabilityClassifier daemonReachabilityClassifier;

    private readonly IDaemonLifecycleStore daemonLifecycleStore;

    private readonly IDaemonProcessIdentityAssessor processIdentityAssessor;

    private readonly IDaemonSessionDiagnosisResolver daemonSessionDiagnosisResolver;

    private readonly IDaemonDiagnosisOutputMapper daemonDiagnosisOutputMapper;

    private readonly IWorktreeProjectPathResolver worktreeProjectPathResolver;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonListQueryService" /> class. </summary>
    /// <param name="gitWorktreeQueryService"> The Git worktree query-service dependency. </param>
    /// <param name="unityProjectResolver"> The Unity-project resolver dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    /// <param name="daemonDiagnosisStore"> The daemon diagnosis-store dependency. </param>
    /// <param name="daemonSessionProbe"> The exact-session probe and token-rotation dependency. </param>
    /// <param name="daemonReachabilityClassifier"> The daemon reachability-classifier dependency. </param>
    /// <param name="daemonLifecycleStore"> The daemon lifecycle observation store dependency. </param>
    /// <param name="processIdentityAssessor"> The daemon process identity assessor dependency. </param>
    /// <param name="daemonSessionDiagnosisResolver"> The daemon session-diagnosis resolver dependency. </param>
    /// <param name="daemonDiagnosisOutputMapper"> The daemon diagnosis-output mapper dependency. </param>
    /// <param name="worktreeProjectPathResolver"> The worktree project-path resolver dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonListQueryService (
        IGitWorktreeQueryService gitWorktreeQueryService,
        IUnityProjectResolver unityProjectResolver,
        IDaemonSessionStore daemonSessionStore,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        DaemonSessionProbe daemonSessionProbe,
        IDaemonReachabilityClassifier daemonReachabilityClassifier,
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonProcessIdentityAssessor processIdentityAssessor,
        IDaemonSessionDiagnosisResolver daemonSessionDiagnosisResolver,
        IDaemonDiagnosisOutputMapper daemonDiagnosisOutputMapper,
        IWorktreeProjectPathResolver worktreeProjectPathResolver,
        TimeProvider timeProvider)
    {
        this.gitWorktreeQueryService = gitWorktreeQueryService ?? throw new ArgumentNullException(nameof(gitWorktreeQueryService));
        this.unityProjectResolver = unityProjectResolver ?? throw new ArgumentNullException(nameof(unityProjectResolver));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.daemonSessionProbe = daemonSessionProbe ?? throw new ArgumentNullException(nameof(daemonSessionProbe));
        this.daemonReachabilityClassifier = daemonReachabilityClassifier ?? throw new ArgumentNullException(nameof(daemonReachabilityClassifier));
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
        this.daemonSessionDiagnosisResolver = daemonSessionDiagnosisResolver ?? throw new ArgumentNullException(nameof(daemonSessionDiagnosisResolver));
        this.daemonDiagnosisOutputMapper = daemonDiagnosisOutputMapper ?? throw new ArgumentNullException(nameof(daemonDiagnosisOutputMapper));
        this.worktreeProjectPathResolver = worktreeProjectPathResolver ?? throw new ArgumentNullException(nameof(worktreeProjectPathResolver));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Resolves daemon registrations across Git worktrees for one Unity project context. </summary>
    /// <param name="unityProject"> The current Unity project context. </param>
    /// <param name="timeout"> The shared daemon-list timeout budget. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-list execution result. </returns>
    public async ValueTask<DaemonListExecutionResult> GetListAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (!TryGetRemainingTimeout(
                deadline,
                "Timed out before Git worktree enumeration could begin.",
                out var gitWorktreeQueryTimeout,
                out var gitWorktreeQueryTimeoutError))
        {
            return DaemonListExecutionResult.Failure(gitWorktreeQueryTimeoutError!);
        }

        var gitWorktreeQueryResult = await gitWorktreeQueryService.GetWorktreeInfoAsync(
                unityProject.UnityProjectRoot,
                gitWorktreeQueryTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!gitWorktreeQueryResult.IsSuccess)
        {
            return DaemonListExecutionResult.Failure(gitWorktreeQueryResult.Error!);
        }

        var gitWorktreeQuery = gitWorktreeQueryResult.Output!;
        var items = new List<DaemonListItemOutput>();
        var orderedWorktrees = gitWorktreeQuery.Worktrees
            .OrderBy(static x => x.WorktreePath, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < orderedWorktrees.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var worktree = orderedWorktrees[index];

            var observationResult = await TryObserveWorktreeAsync(
                    worktree,
                    gitWorktreeQuery.ProjectRelativePath,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!observationResult.IsSuccess)
            {
                if (observationResult.Error!.Kind == ExecutionErrorKind.Timeout)
                {
                    return DaemonListExecutionResult.Success(CreatePartialOutput(
                        timeout,
                        gitWorktreeQuery.ProjectRelativePath,
                        items,
                        orderedWorktrees.Length - index));
                }

                return DaemonListExecutionResult.Failure(observationResult.Error!);
            }

            if (observationResult.Item != null)
            {
                items.Add(observationResult.Item);
            }
        }

        return DaemonListExecutionResult.Success(CreateCompleteOutput(
            timeout,
            gitWorktreeQuery.ProjectRelativePath,
            items));
    }

    /// <summary> Reads daemon session and probe state for one worktree candidate. </summary>
    /// <param name="worktree"> The Git worktree metadata. </param>
    /// <param name="projectRelativePath"> The current Unity project path relative to the current worktree root. </param>
    /// <param name="deadline"> The shared daemon-list execution deadline. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The observed daemon-list item result. </returns>
    private async ValueTask<WorktreeObservationResult> TryObserveWorktreeAsync (
        GitWorktreeInfo worktree,
        string projectRelativePath,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var candidateProjectPath = worktreeProjectPathResolver.ResolveCandidateProjectPath(worktree.WorktreePath, projectRelativePath);
        var candidateProjectResult = unityProjectResolver.Resolve(new ProjectPathCandidate(
            candidateProjectPath,
            UnityProjectPathSource.Fallback,
            "gitWorktree.projectRelativePath"));
        if (!candidateProjectResult.IsSuccess)
        {
            return WorktreeObservationResult.Success(item: null);
        }

        var candidateProject = candidateProjectResult.Context!;
        var sessionReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before daemon session read could begin.",
                "Timed out while reading daemon session.",
                token => daemonSessionStore.ReadAsync(
                    candidateProject.RepositoryRoot,
                    candidateProject.ProjectFingerprint,
                    token))
            .ConfigureAwait(false);
        if (!sessionReadOperation.IsSuccess)
        {
            return WorktreeObservationResult.Failure(sessionReadOperation.Error!);
        }

        var sessionReadResult = sessionReadOperation.Value!;

        if (!sessionReadResult.IsSuccess)
        {
            return WorktreeObservationResult.Success(CreateSessionReadFailureItem(worktree, candidateProject, sessionReadResult));
        }

        if (!sessionReadResult.Exists)
        {
            return WorktreeObservationResult.Success(item: null);
        }

        return await ProbeDaemonSessionAsync(
                worktree,
                candidateProject,
                sessionReadResult.Session!,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Probes one valid daemon session for reachability. </summary>
    /// <param name="worktree"> The Git worktree metadata. </param>
    /// <param name="candidateProject"> The resolved Unity project context for the candidate worktree. </param>
    /// <param name="session"> The valid daemon session metadata. </param>
    /// <param name="deadline"> The shared daemon-list execution deadline. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The observed daemon-list item result. </returns>
    private async ValueTask<WorktreeObservationResult> ProbeDaemonSessionAsync (
        GitWorktreeInfo worktree,
        ResolvedUnityProjectContext candidateProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var probeResult = await daemonSessionProbe.ProbeAsync(
                candidateProject,
                session,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (probeResult.SessionReadFailure is not null)
        {
            return WorktreeObservationResult.Success(
                CreateSessionReadFailureItem(
                    worktree,
                    candidateProject,
                    probeResult.SessionReadFailure));
        }

        var probedSession = probeResult.Session;
        if (probeResult.IsSuccess)
        {
            var observation = StatusDaemonObservationCodec.CreateFromPing(
                DaemonStatusKind.Running,
                probeResult.PingResponse);

            return WorktreeObservationResult.Success(CreateItem(
                worktree,
                candidateProject,
                DaemonListItemState.Running,
                null,
                probedSession,
                observation,
                diagnosis: null));
        }

        var probeFailure = probeResult.ProbeFailure!;
        if (daemonReachabilityClassifier.IsRequestTimeout(probeFailure))
        {
            if (deadline.IsExpired)
            {
                return WorktreeObservationResult.Failure(ExecutionError.Timeout(
                    "Timed out while probing daemon session."));
            }

            return WorktreeObservationResult.Success(CreateItem(
                worktree,
                candidateProject,
                DaemonListItemState.Error,
                DaemonListItemReason.ProbeTimeout,
                probedSession,
                StatusDaemonObservationCodec.CreateWithoutPing(DaemonStatusKind.Stale),
                diagnosis: null));
        }

        if (daemonReachabilityClassifier.IsNotRunning(probeFailure))
        {
            var unreachableSessionResolution = await ResolveUnreachableSessionAsync(
                    candidateProject,
                    probedSession,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!unreachableSessionResolution.IsSuccess)
            {
                return WorktreeObservationResult.Failure(unreachableSessionResolution.Error!);
            }

            return WorktreeObservationResult.Success(CreateItem(
                worktree,
                candidateProject,
                DaemonListItemState.Stale,
                DaemonListItemReason.StaleSession,
                probedSession,
                StatusDaemonObservationCodec.CreateWithoutPing(DaemonStatusKind.Stale),
                unreachableSessionResolution.Diagnosis));
        }

        return WorktreeObservationResult.Success(CreateItem(
            worktree,
            candidateProject,
            DaemonListItemState.Error,
            DaemonListItemReason.ProbeFailed,
            probedSession,
            StatusDaemonObservationCodec.CreateWithoutPing(DaemonStatusKind.Stale),
            diagnosis: null));
    }

    /// <summary> Gets remaining timeout from the shared execution deadline. </summary>
    /// <param name="deadline"> The shared execution deadline. </param>
    /// <param name="timeoutMessage"> The timeout message to emit when budget is exhausted. </param>
    /// <param name="remainingTimeout"> The remaining timeout budget. </param>
    /// <param name="error"> The timeout error when budget is exhausted; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when remaining timeout is available; otherwise <see langword="false" />. </returns>
    private static bool TryGetRemainingTimeout (
        ExecutionDeadline deadline,
        string timeoutMessage,
        out TimeSpan remainingTimeout,
        out ExecutionError? error)
    {
        if (deadline.TryGetRemainingTimeout(out remainingTimeout))
        {
            error = null;
            return true;
        }

        error = ExecutionError.Timeout(timeoutMessage);
        return false;
    }

    /// <summary> Creates one daemon-list item for session read failures. </summary>
    /// <param name="worktree"> The Git worktree metadata. </param>
    /// <param name="candidateProject"> The resolved Unity project context for the candidate worktree. </param>
    /// <param name="sessionReadResult"> The daemon session read result. </param>
    /// <returns> The failed daemon-list item. </returns>
    private static DaemonListItemOutput CreateSessionReadFailureItem (
        GitWorktreeInfo worktree,
        ResolvedUnityProjectContext candidateProject,
        DaemonSessionReadResult sessionReadResult)
    {
        var reason = sessionReadResult.FailureKind == DaemonSessionReadFailureKind.InvalidSession
            ? DaemonListItemReason.InvalidSession
            : DaemonListItemReason.ProbeFailed;
        return CreateItem(
            worktree,
            candidateProject,
            DaemonListItemState.Error,
            reason,
            session: null,
            observation: null,
            diagnosis: null);
    }

    /// <summary> Creates one daemon-list item from worktree, project, and optional session values. </summary>
    /// <param name="worktree"> The Git worktree metadata. </param>
    /// <param name="candidateProject"> The resolved Unity project context for the candidate worktree. </param>
    /// <param name="state"> The daemon-list state literal. </param>
    /// <param name="reason"> The daemon-list reason literal when applicable. </param>
    /// <param name="session"> The valid daemon session metadata when available; otherwise <see langword="null" />. </param>
    /// <param name="diagnosis"> The daemon diagnosis values when available; otherwise <see langword="null" />. </param>
    /// <returns> The daemon-list item output. </returns>
    private static DaemonListItemOutput CreateItem (
        GitWorktreeInfo worktree,
        ResolvedUnityProjectContext candidateProject,
        DaemonListItemState state,
        DaemonListItemReason? reason,
        DaemonSession? session,
        StatusDaemonObservation? observation,
        DaemonDiagnosisOutput? diagnosis)
    {
        return new DaemonListItemOutput(
            WorktreePath: worktree.WorktreePath,
            BranchRef: worktree.BranchRef,
            Head: worktree.Head,
            ProjectPath: candidateProject.UnityProjectRoot,
            ProjectFingerprint: candidateProject.ProjectFingerprint,
            State: state,
            Reason: reason,
            IssuedAtUtc: session?.IssuedAtUtc,
            ProcessId: session?.ProcessId,
            ProcessStartedAtUtc: session?.ProcessStartedAtUtc,
            EditorMode: session?.EditorMode,
            OwnerKind: session?.OwnerKind,
            CanShutdownProcess: session?.CanShutdownProcess,
            EndpointTransportKind: session?.Endpoint.TransportKind,
            EndpointAddress: session?.Endpoint.Address,
            LifecycleState: observation?.LifecycleState,
            BlockingReason: observation?.BlockingReason,
            CompileState: observation?.CompileState,
            Generations: observation?.Generations,
            CanAcceptExecutionRequests: observation?.CanAcceptExecutionRequests,
            ObservedAtUtc: observation?.ObservedAtUtc,
            ActionRequired: observation?.ActionRequired,
            PrimaryDiagnostic: observation?.PrimaryDiagnostic,
            Diagnosis: diagnosis);
    }

    private async ValueTask<UnreachableSessionResolution> ResolveUnreachableSessionAsync (
        ResolvedUnityProjectContext candidateProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var lifecycleReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before daemon lifecycle read could begin.",
                "Timed out while reading daemon lifecycle observation.",
                token => daemonLifecycleStore.ReadAsync(
                    candidateProject.RepositoryRoot,
                    candidateProject.ProjectFingerprint,
                    token))
            .ConfigureAwait(false);
        if (!lifecycleReadOperation.IsSuccess)
        {
            return UnreachableSessionResolution.Failure(lifecycleReadOperation.Error!);
        }

        var lifecycleReadResult = lifecycleReadOperation.Value!;
        var observation = lifecycleReadResult.Observation;
        var hasUsableRecoveringObservation = lifecycleReadResult.IsSuccess
            && lifecycleReadResult.Exists
            && observation is not null
            && observation.IsRecovering
            && DaemonLifecycleObservationAvailability.IsUsableForSession(
                observation,
                session,
                processIdentityAssessor,
                timeProvider);
        if (hasUsableRecoveringObservation)
        {
            return UnreachableSessionResolution.Success(diagnosis: null);
        }

        var diagnosisReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before stale daemon diagnosis read could begin.",
                "Timed out while reading stale daemon diagnosis.",
                token => daemonDiagnosisStore.ReadAsync(
                    candidateProject.RepositoryRoot,
                    candidateProject.ProjectFingerprint,
                    token))
            .ConfigureAwait(false);
        if (!diagnosisReadOperation.IsSuccess)
        {
            return UnreachableSessionResolution.Failure(diagnosisReadOperation.Error!);
        }

        var diagnosisReadResult = diagnosisReadOperation.Value!;
        var persistedDiagnosis = diagnosisReadResult.IsSuccess
            ? diagnosisReadResult.Diagnosis
            : null;

        var diagnosisResolution = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before stale daemon diagnosis resolution could begin.",
                "Timed out while resolving stale daemon diagnosis.",
                token => daemonSessionDiagnosisResolver.ResolveForSessionAsync(
                    candidateProject,
                    session,
                    persistedDiagnosis,
                    token))
            .ConfigureAwait(false);
        if (!diagnosisResolution.IsSuccess)
        {
            return UnreachableSessionResolution.Failure(diagnosisResolution.Error!);
        }

        var diagnosis = diagnosisResolution.Value;
        return diagnosis is null
            ? UnreachableSessionResolution.Success(diagnosis: null)
            : UnreachableSessionResolution.Success(daemonDiagnosisOutputMapper.ToOutput(diagnosis));
    }

    /// <summary> Creates one complete daemon-list execution output. </summary>
    /// <param name="timeout"> The requested shared timeout budget. </param>
    /// <param name="projectRelativePath"> The current project path relative to the current worktree root. </param>
    /// <param name="items"> The observed daemon-list items. </param>
    /// <returns> The complete daemon-list execution output. </returns>
    private static DaemonListExecutionOutput CreateCompleteOutput (
        TimeSpan timeout,
        string projectRelativePath,
        IReadOnlyList<DaemonListItemOutput> items)
    {
        return new DaemonListExecutionOutput(
            TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds),
            ProjectRelativePath: projectRelativePath,
            IsComplete: true,
            CompletionReason: null,
            RemainingWorktreeCount: 0,
            Items: items);
    }

    /// <summary> Creates one partial daemon-list execution output after shared timeout exhaustion. </summary>
    /// <param name="timeout"> The requested shared timeout budget. </param>
    /// <param name="projectRelativePath"> The current project path relative to the current worktree root. </param>
    /// <param name="items"> The observed daemon-list items completed before timeout. </param>
    /// <param name="remainingWorktreeCount"> The number of worktrees left unobserved. </param>
    /// <returns> The partial daemon-list execution output. </returns>
    private static DaemonListExecutionOutput CreatePartialOutput (
        TimeSpan timeout,
        string projectRelativePath,
        IReadOnlyList<DaemonListItemOutput> items,
        int remainingWorktreeCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(remainingWorktreeCount);

        return new DaemonListExecutionOutput(
            TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds),
            ProjectRelativePath: projectRelativePath,
            IsComplete: false,
            CompletionReason: DaemonListCompletionReason.Timeout,
            RemainingWorktreeCount: remainingWorktreeCount,
            Items: items);
    }

    /// <summary> Represents one worktree observation result. </summary>
    /// <param name="Item"> The observed daemon-list item; otherwise <see langword="null" /> when no item should be emitted. </param>
    /// <param name="Error"> The execution error when observation failed; otherwise <see langword="null" />. </param>
    private readonly record struct WorktreeObservationResult (
        DaemonListItemOutput? Item,
        ExecutionError? Error)
    {
        /// <summary> Gets a value indicating whether observation succeeded. </summary>
        public bool IsSuccess => Error is null;

        /// <summary> Creates a successful observation result. </summary>
        /// <param name="item"> The observed item; otherwise <see langword="null" />. </param>
        /// <returns> The successful observation result. </returns>
        public static WorktreeObservationResult Success (DaemonListItemOutput? item)
        {
            return new WorktreeObservationResult(item, null);
        }

        /// <summary> Creates a failed observation result. </summary>
        /// <param name="error"> The structured execution error. </param>
        /// <returns> The failed observation result. </returns>
        public static WorktreeObservationResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new WorktreeObservationResult(null, error);
        }
    }

    private readonly record struct UnreachableSessionResolution (
        DaemonDiagnosisOutput? Diagnosis,
        ExecutionError? Error)
    {
        public bool IsSuccess => Error is null;

        public static UnreachableSessionResolution Success (DaemonDiagnosisOutput? diagnosis)
        {
            return new UnreachableSessionResolution(diagnosis, null);
        }

        public static UnreachableSessionResolution Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new UnreachableSessionResolution(null, error);
        }
    }

}
