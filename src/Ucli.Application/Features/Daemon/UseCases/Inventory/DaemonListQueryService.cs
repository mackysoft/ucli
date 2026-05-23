using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Observation;
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

    private readonly IDaemonPingInfoClient daemonPingInfoClient;

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
    /// <param name="daemonPingInfoClient"> The daemon ping-info client dependency. </param>
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
        IDaemonPingInfoClient daemonPingInfoClient,
        IDaemonReachabilityClassifier daemonReachabilityClassifier,
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonProcessIdentityAssessor processIdentityAssessor,
        IDaemonSessionDiagnosisResolver daemonSessionDiagnosisResolver,
        IDaemonDiagnosisOutputMapper daemonDiagnosisOutputMapper,
        IWorktreeProjectPathResolver worktreeProjectPathResolver,
        TimeProvider? timeProvider = null)
    {
        this.gitWorktreeQueryService = gitWorktreeQueryService ?? throw new ArgumentNullException(nameof(gitWorktreeQueryService));
        this.unityProjectResolver = unityProjectResolver ?? throw new ArgumentNullException(nameof(unityProjectResolver));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonDiagnosisStore = daemonDiagnosisStore ?? throw new ArgumentNullException(nameof(daemonDiagnosisStore));
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.daemonReachabilityClassifier = daemonReachabilityClassifier ?? throw new ArgumentNullException(nameof(daemonReachabilityClassifier));
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
        this.daemonSessionDiagnosisResolver = daemonSessionDiagnosisResolver ?? throw new ArgumentNullException(nameof(daemonSessionDiagnosisResolver));
        this.daemonDiagnosisOutputMapper = daemonDiagnosisOutputMapper ?? throw new ArgumentNullException(nameof(daemonDiagnosisOutputMapper));
        this.worktreeProjectPathResolver = worktreeProjectPathResolver ?? throw new ArgumentNullException(nameof(worktreeProjectPathResolver));
        this.timeProvider = timeProvider ?? TimeProvider.System;
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
        if (!TryGetRemainingTimeout(
                deadline,
                "Timed out before daemon session read could begin.",
                out var sessionReadTimeout,
                out var sessionReadTimeoutError))
        {
            return WorktreeObservationResult.Failure(sessionReadTimeoutError!);
        }

        using var sessionReadCancellationScope = TimeProviderCancellationScope.CreateLinked(
            cancellationToken,
            sessionReadTimeout,
            timeProvider);

        DaemonSessionReadResult sessionReadResult;
        try
        {
            sessionReadResult = await daemonSessionStore.ReadAsync(
                    candidateProject.RepositoryRoot,
                    candidateProject.ProjectFingerprint,
                    sessionReadCancellationScope.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (sessionReadCancellationScope.HasTimedOut
            && !cancellationToken.IsCancellationRequested)
        {
            return WorktreeObservationResult.Failure(ExecutionError.Timeout(
                "Timed out while reading daemon session."));
        }

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
        if (!TryGetRemainingTimeout(
                deadline,
                "Timed out before daemon session probe could begin.",
                out var probeTimeout,
                out var probeTimeoutError))
        {
            return WorktreeObservationResult.Failure(probeTimeoutError!);
        }

        using var probeCancellationScope = TimeProviderCancellationScope.CreateLinked(
            cancellationToken,
            probeTimeout,
            timeProvider);

        try
        {
            var pingResponse = await daemonPingInfoClient.PingAndReadAsync(
                    candidateProject,
                    probeTimeout,
                    session.SessionToken,
                    cancellationToken: probeCancellationScope.Token)
                .ConfigureAwait(false);
            var observation = StatusDaemonObservationCodec.CreateFromPing(
                DaemonStatusKind.Running,
                pingResponse);

            return WorktreeObservationResult.Success(CreateItem(
                worktree,
                candidateProject,
                DaemonListItemState.Running,
                null,
                session,
                observation,
                diagnosis: null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (probeCancellationScope.HasTimedOut
            && !cancellationToken.IsCancellationRequested)
        {
            return WorktreeObservationResult.Failure(ExecutionError.Timeout(
                "Timed out while probing daemon session."));
        }
        catch (TimeoutException)
        {
            if (deadline.IsExpired)
            {
                return WorktreeObservationResult.Failure(ExecutionError.Timeout(
                    "Timed out while probing daemon session."));
            }

            var observation = await CreateUnreachableObservationAsync(
                    candidateProject,
                    session,
                    cancellationToken)
                .ConfigureAwait(false);
            if (observation.DaemonStatus == DaemonStatusKind.Running)
            {
                return WorktreeObservationResult.Success(CreateItem(
                    worktree,
                    candidateProject,
                    DaemonListItemState.Running,
                    null,
                    session,
                    observation,
                    diagnosis: null));
            }

            return WorktreeObservationResult.Success(CreateItem(
                worktree,
                candidateProject,
                DaemonListItemState.Error,
                DaemonListItemReason.ProbeTimeout,
                session,
                observation,
                diagnosis: null));
        }
        catch (Exception exception) when (daemonReachabilityClassifier.IsNotRunning(exception))
        {
            var observation = await CreateUnreachableObservationAsync(
                    candidateProject,
                    session,
                    cancellationToken)
                .ConfigureAwait(false);
            if (observation.DaemonStatus == DaemonStatusKind.Running)
            {
                return WorktreeObservationResult.Success(CreateItem(
                    worktree,
                    candidateProject,
                    DaemonListItemState.Running,
                    null,
                    session,
                    observation,
                    diagnosis: null));
            }

            var diagnosis = await ResolveStaleDiagnosisAsync(
                    candidateProject,
                    session,
                    cancellationToken)
                .ConfigureAwait(false);
            return WorktreeObservationResult.Success(CreateItem(
                worktree,
                candidateProject,
                DaemonListItemState.Stale,
                DaemonListItemReason.StaleSession,
                session,
                observation,
                diagnosis));
        }
        catch (Exception)
        {
            return WorktreeObservationResult.Success(CreateItem(
                worktree,
                candidateProject,
                DaemonListItemState.Error,
                DaemonListItemReason.ProbeFailed,
                session,
                StatusDaemonObservationCodec.CreateUnavailable(DaemonStatusKind.Stale),
                diagnosis: null));
        }
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
            EndpointTransportKind: session?.EndpointTransportKind,
            EndpointAddress: session?.EndpointAddress,
            LifecycleState: observation?.LifecycleState,
            BlockingReason: observation?.BlockingReason,
            CompileState: observation?.CompileState,
            CompileGeneration: observation?.CompileGeneration,
            DomainReloadGeneration: observation?.DomainReloadGeneration,
            CanAcceptExecutionRequests: observation?.CanAcceptExecutionRequests,
            ObservedAtUtc: observation?.ObservedAtUtc,
            ActionRequired: observation?.ActionRequired,
            PrimaryDiagnostic: observation?.PrimaryDiagnostic,
            Diagnosis: diagnosis);
    }

    private async ValueTask<StatusDaemonObservation> CreateUnreachableObservationAsync (
        ResolvedUnityProjectContext candidateProject,
        DaemonSession session,
        CancellationToken cancellationToken)
    {
        var lifecycleReadResult = await daemonLifecycleStore.ReadAsync(
                candidateProject.RepositoryRoot,
                candidateProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (lifecycleReadResult.IsSuccess
            && lifecycleReadResult.Exists
            && lifecycleReadResult.Observation!.IsRecovering
            && DaemonLifecycleObservationMatcher.MatchesSessionByEditorInstance(lifecycleReadResult.Observation, session)
            && IsMatchingLiveProcess(session))
        {
            return StatusDaemonObservationCodec.CreateFromLifecycleObservation(
                DaemonStatusKind.Running,
                lifecycleReadResult.Observation);
        }

        return StatusDaemonObservationCodec.CreateUnavailable(DaemonStatusKind.Stale);
    }

    private bool IsMatchingLiveProcess (DaemonSession session)
    {
        if (session.ProcessId is not int processId)
        {
            return false;
        }

        return processIdentityAssessor.AssessByProcessId(processId, session.ProcessStartedAtUtc).Status
            == DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess;
    }

    /// <summary> Resolves diagnosis payload for one stale daemon session when available. </summary>
    /// <param name="candidateProject"> The resolved Unity project context for the candidate worktree. </param>
    /// <param name="session"> The stale daemon session metadata. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon diagnosis payload when available; otherwise <see langword="null" />. </returns>
    private async ValueTask<DaemonDiagnosisOutput?> ResolveStaleDiagnosisAsync (
        ResolvedUnityProjectContext candidateProject,
        DaemonSession session,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(candidateProject);
        ArgumentNullException.ThrowIfNull(session);

        var diagnosisReadResult = await daemonDiagnosisStore.ReadAsync(
                candidateProject.RepositoryRoot,
                candidateProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        var persistedDiagnosis = diagnosisReadResult.IsSuccess
            ? diagnosisReadResult.Diagnosis
            : null;

        var diagnosis = await daemonSessionDiagnosisResolver.ResolveForSessionAsync(
                candidateProject,
                session,
                persistedDiagnosis,
                cancellationToken)
            .ConfigureAwait(false);
        return diagnosis is null
            ? null
            : daemonDiagnosisOutputMapper.ToOutput(diagnosis);
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
}
