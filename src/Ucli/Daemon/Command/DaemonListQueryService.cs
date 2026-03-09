using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Git;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Daemon.Command;

/// <summary> Implements daemon registration enumeration across Git worktrees. </summary>
internal sealed class DaemonListQueryService : IDaemonListQueryService
{
    private const string StaleSessionMessage = "Daemon session exists but daemon is not reachable.";

    private readonly IGitWorktreeQueryService gitWorktreeQueryService;

    private readonly IUnityProjectResolver unityProjectResolver;

    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonPingClient daemonPingClient;

    private readonly IDaemonReachabilityClassifier daemonReachabilityClassifier;

    /// <summary> Initializes a new instance of the <see cref="DaemonListQueryService" /> class. </summary>
    /// <param name="gitWorktreeQueryService"> The Git worktree query-service dependency. </param>
    /// <param name="unityProjectResolver"> The Unity-project resolver dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session-store dependency. </param>
    /// <param name="daemonPingClient"> The daemon ping-client dependency. </param>
    /// <param name="daemonReachabilityClassifier"> The daemon reachability-classifier dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonListQueryService (
        IGitWorktreeQueryService gitWorktreeQueryService,
        IUnityProjectResolver unityProjectResolver,
        IDaemonSessionStore daemonSessionStore,
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier daemonReachabilityClassifier)
    {
        this.gitWorktreeQueryService = gitWorktreeQueryService ?? throw new ArgumentNullException(nameof(gitWorktreeQueryService));
        this.unityProjectResolver = unityProjectResolver ?? throw new ArgumentNullException(nameof(unityProjectResolver));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.daemonReachabilityClassifier = daemonReachabilityClassifier ?? throw new ArgumentNullException(nameof(daemonReachabilityClassifier));
    }

    /// <summary> Resolves daemon registrations across Git worktrees for one Unity project context. </summary>
    /// <param name="unityProject"> The current Unity project context. </param>
    /// <param name="timeout"> The shared daemon-list timeout budget. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon-list execution result. </returns>
    public async ValueTask<DaemonListExecutionResult> GetList (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout);
        if (!TryGetRemainingTimeout(
                deadline,
                "Timed out before Git worktree enumeration could begin.",
                out var gitWorktreeQueryTimeout,
                out var gitWorktreeQueryTimeoutError))
        {
            return DaemonListExecutionResult.Failure(gitWorktreeQueryTimeoutError!);
        }

        var gitWorktreeQueryResult = await gitWorktreeQueryService.GetWorktreeInfo(
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
        foreach (var worktree in gitWorktreeQuery.Worktrees.OrderBy(static x => x.WorktreePath, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var observationResult = await TryObserveWorktree(
                    worktree,
                    gitWorktreeQuery.ProjectRelativePath,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!observationResult.IsSuccess)
            {
                return DaemonListExecutionResult.Failure(observationResult.Error!);
            }

            if (observationResult.Item != null)
            {
                items.Add(observationResult.Item);
            }
        }

        return DaemonListExecutionResult.Success(new DaemonListExecutionOutput(
            TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds),
            ProjectRelativePath: gitWorktreeQuery.ProjectRelativePath,
            Items: items));
    }

    /// <summary> Reads daemon session and probe state for one worktree candidate. </summary>
    /// <param name="worktree"> The Git worktree metadata. </param>
    /// <param name="projectRelativePath"> The current Unity project path relative to the current worktree root. </param>
    /// <param name="deadline"> The shared daemon-list execution deadline. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The observed daemon-list item result. </returns>
    private async ValueTask<WorktreeObservationResult> TryObserveWorktree (
        GitWorktreeInfo worktree,
        string projectRelativePath,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var candidateProjectPath = ResolveCandidateProjectPath(worktree.WorktreePath, projectRelativePath);
        var candidateProjectResult = unityProjectResolver.Resolve(candidateProjectPath);
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

        using var sessionReadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        sessionReadCancellationTokenSource.CancelAfter(sessionReadTimeout);

        DaemonSessionReadResult sessionReadResult;
        try
        {
            sessionReadResult = await daemonSessionStore.Read(
                    candidateProject.RepositoryRoot,
                    candidateProject.ProjectFingerprint,
                    sessionReadCancellationTokenSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (sessionReadCancellationTokenSource.IsCancellationRequested)
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

        return await ProbeDaemonSession(
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
    private async ValueTask<WorktreeObservationResult> ProbeDaemonSession (
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

        using var probeCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCancellationTokenSource.CancelAfter(probeTimeout);

        try
        {
            await daemonPingClient.Ping(
                    candidateProject,
                    probeTimeout,
                    session.SessionToken,
                    probeCancellationTokenSource.Token)
                .ConfigureAwait(false);

            return WorktreeObservationResult.Success(CreateItem(
                worktree,
                candidateProject,
                DaemonListStateCodec.Running,
                null,
                session,
                message: null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (probeCancellationTokenSource.IsCancellationRequested)
        {
            return WorktreeObservationResult.Failure(ExecutionError.Timeout(
                "Timed out while probing daemon session."));
        }
        catch (TimeoutException exception)
        {
            if (deadline.IsExpired)
            {
                return WorktreeObservationResult.Failure(ExecutionError.Timeout(
                    "Timed out while probing daemon session."));
            }

            return WorktreeObservationResult.Success(CreateItem(
                worktree,
                candidateProject,
                DaemonListStateCodec.Error,
                DaemonListReasonCodec.ProbeTimeout,
                session,
                exception.Message));
        }
        catch (Exception exception) when (daemonReachabilityClassifier.IsNotRunning(exception))
        {
            return WorktreeObservationResult.Success(CreateItem(
                worktree,
                candidateProject,
                DaemonListStateCodec.Stale,
                DaemonListReasonCodec.StaleSession,
                session,
                StaleSessionMessage));
        }
        catch (Exception exception)
        {
            return WorktreeObservationResult.Success(CreateItem(
                worktree,
                candidateProject,
                DaemonListStateCodec.Error,
                DaemonListReasonCodec.ProbeFailed,
                session,
                exception.Message));
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
            ? DaemonListReasonCodec.InvalidSession
            : DaemonListReasonCodec.ProbeFailed;
        return CreateItem(
            worktree,
            candidateProject,
            DaemonListStateCodec.Error,
            reason,
            session: null,
            sessionReadResult.Error!.Message);
    }

    /// <summary> Creates one daemon-list item from worktree, project, and optional session values. </summary>
    /// <param name="worktree"> The Git worktree metadata. </param>
    /// <param name="candidateProject"> The resolved Unity project context for the candidate worktree. </param>
    /// <param name="state"> The daemon-list state literal. </param>
    /// <param name="reason"> The daemon-list reason literal when applicable. </param>
    /// <param name="session"> The valid daemon session metadata when available; otherwise <see langword="null" />. </param>
    /// <param name="message"> The diagnostic message when available; otherwise <see langword="null" />. </param>
    /// <returns> The daemon-list item output. </returns>
    private static DaemonListItemOutput CreateItem (
        GitWorktreeInfo worktree,
        ResolvedUnityProjectContext candidateProject,
        string state,
        string? reason,
        DaemonSession? session,
        string? message)
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
            EndpointTransportKind: session?.EndpointTransportKind,
            EndpointAddress: session?.EndpointAddress,
            Message: message);
    }

    /// <summary> Resolves one candidate Unity project path for the specified worktree root. </summary>
    /// <param name="worktreePath"> The Git worktree root path. </param>
    /// <param name="projectRelativePath"> The relative project path to append. </param>
    /// <returns> The candidate Unity project path. </returns>
    private static string ResolveCandidateProjectPath (
        string worktreePath,
        string projectRelativePath)
    {
        return string.Equals(projectRelativePath, ".", StringComparison.Ordinal)
            ? worktreePath
            : Path.Combine(worktreePath, projectRelativePath);
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