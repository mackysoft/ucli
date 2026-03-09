using System.Net.Sockets;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Git;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonListQueryServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WithMixedWorktrees_ReturnsSortedRunningItemsAndSkipsMissingSessions ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var worktreeA = CreateUnityProject("/repo/wt-a", "UnityProject", "fp-a");
        var worktreeB = CreateUnityProject("/repo/wt-b", "UnityProject", "fp-b");
        var gitWorktreeQueryService = new StubGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
            CurrentWorktreeRoot: currentProject.RepositoryRoot,
            ProjectRelativePath: "UnityProject",
            Worktrees:
            [
                new GitWorktreeInfo("/repo/wt-b", "bbbbbbbb", "refs/heads/feature/worktree-b"),
                new GitWorktreeInfo("/repo/wt-missing", "mmmmmmmm", "refs/heads/missing"),
                new GitWorktreeInfo("/repo/wt-a", "aaaaaaaa", null),
                new GitWorktreeInfo("/repo/wt-current", "cccccccc", "refs/heads/main"),
            ])));
        var unityProjectResolver = new StubUnityProjectResolver(
            currentProject,
            worktreeA,
            worktreeB);
        var sessionStore = new StubDaemonSessionStore((storageRoot, projectFingerprint) =>
        {
            return projectFingerprint switch
            {
                "fp-current" => DaemonSessionReadResult.Success(null),
                "fp-a" => DaemonSessionReadResult.Success(CreateSession(projectFingerprint, "endpoint-a", 1001)),
                "fp-b" => DaemonSessionReadResult.Success(CreateSession(projectFingerprint, "endpoint-b", 1002)),
                _ => throw new InvalidOperationException($"Unexpected fingerprint: {projectFingerprint}"),
            };
        });
        var pingClient = new StubDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask);
        var service = new DaemonListQueryService(
            gitWorktreeQueryService,
            unityProjectResolver,
            sessionStore,
            pingClient,
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetList(currentProject, TimeSpan.FromMilliseconds(2500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.Equal(2500, output.TimeoutMilliseconds);
        Assert.Equal("UnityProject", output.ProjectRelativePath);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        Assert.Collection(
            output.Items,
            item =>
            {
                Assert.Equal("/repo/wt-a", item.WorktreePath);
                Assert.Null(item.BranchRef);
                Assert.Equal("aaaaaaaa", item.Head);
                Assert.Equal(worktreeA.UnityProjectRoot, item.ProjectPath);
                Assert.Equal("fp-a", item.ProjectFingerprint);
                Assert.Equal(DaemonListStateCodec.Running, item.State);
                Assert.Null(item.Reason);
                Assert.Equal(1001, item.ProcessId);
                Assert.Equal("endpoint-a", item.EndpointAddress);
                Assert.Null(item.Message);
            },
            item =>
            {
                Assert.Equal("/repo/wt-b", item.WorktreePath);
                Assert.Equal("refs/heads/feature/worktree-b", item.BranchRef);
                Assert.Equal("bbbbbbbb", item.Head);
                Assert.Equal(worktreeB.UnityProjectRoot, item.ProjectPath);
                Assert.Equal("fp-b", item.ProjectFingerprint);
                Assert.Equal(DaemonListStateCodec.Running, item.State);
                Assert.Null(item.Reason);
                Assert.Equal(1002, item.ProcessId);
                Assert.Equal("endpoint-b", item.EndpointAddress);
                Assert.Null(item.Message);
            });

        Assert.Equal([currentProject.UnityProjectRoot], gitWorktreeQueryService.QueryPaths);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenGitWorktreeQueryFails_ReturnsFailure ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var service = new DaemonListQueryService(
            new StubGitWorktreeQueryService(GitWorktreeQueryResult.Failure(ExecutionError.InternalError("git failed"))),
            new StubUnityProjectResolver(currentProject),
            new StubDaemonSessionStore(static (_, _) => DaemonSessionReadResult.Success(null)),
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetList(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Equal("git failed", result.Error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenSessionIsInvalid_ReturnsInvalidSessionItemWithoutSessionDerivedFields ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Failure(
                ExecutionError.InvalidArgument("Daemon session JSON is invalid."),
                DaemonSessionReadFailureKind.InvalidSession),
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetList(currentProject, TimeSpan.FromMilliseconds(3000), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListStateCodec.Error, item.State);
        Assert.Equal(DaemonListReasonCodec.InvalidSession, item.Reason);
        Assert.Null(item.IssuedAtUtc);
        Assert.Null(item.ProcessId);
        Assert.Null(item.EndpointTransportKind);
        Assert.Null(item.EndpointAddress);
        Assert.Equal("Daemon session JSON is invalid.", item.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeTimesOut_ReturnsProbeTimeoutItem ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = CreateSession("fp-current", "endpoint-timeout", 2100);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new TimeoutException("probe timed out"))),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetList(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListStateCodec.Error, item.State);
        Assert.Equal(DaemonListReasonCodec.ProbeTimeout, item.Reason);
        Assert.Equal(2100, item.ProcessId);
        Assert.Equal("endpoint-timeout", item.EndpointAddress);
        Assert.Equal("probe timed out", item.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenSharedDeadlineExpiresDuringProbe_ReturnsPartialSuccess ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var worktreeA = CreateUnityProject("/repo/wt-a", "UnityProject", "fp-a");
        var worktreeB = CreateUnityProject("/repo/wt-b", "UnityProject", "fp-b");
        var gitWorktreeQueryService = new StubGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
            CurrentWorktreeRoot: currentProject.RepositoryRoot,
            ProjectRelativePath: "UnityProject",
            Worktrees:
            [
                new GitWorktreeInfo("/repo/wt-a", "aaaaaaaa", "refs/heads/a"),
                new GitWorktreeInfo("/repo/wt-b", "bbbbbbbb", "refs/heads/b"),
            ])));
        var unityProjectResolver = new StubUnityProjectResolver(worktreeA, worktreeB);
        var sessionStore = new StubDaemonSessionStore((_, projectFingerprint) => projectFingerprint switch
        {
            "fp-a" => DaemonSessionReadResult.Success(CreateSession("fp-a", "endpoint-a", 2401)),
            "fp-b" => DaemonSessionReadResult.Success(CreateSession("fp-b", "endpoint-b", 2402)),
            _ => throw new InvalidOperationException($"Unexpected fingerprint: {projectFingerprint}"),
        });
        var service = new DaemonListQueryService(
            gitWorktreeQueryService,
            unityProjectResolver,
            sessionStore,
            new StubDaemonPingClient(async (unityProject, _, _, cancellationToken) =>
            {
                if (unityProject.ProjectFingerprint == "fp-b")
                {
                    await Task.Delay(50, cancellationToken);
                    throw new TimeoutException("probe timed out");
                }
            }),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetList(currentProject, TimeSpan.FromMilliseconds(10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.False(output.IsComplete);
        Assert.Equal(DaemonListCompletionReasonCodec.Timeout, output.CompletionReason);
        Assert.Equal(1, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal("/repo/wt-a", item.WorktreePath);
        Assert.Equal(DaemonListStateCodec.Running, item.State);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeClassifiesNotRunning_ReturnsStaleItem ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = CreateSession("fp-current", "endpoint-stale", 2200);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            new StubDaemonReachabilityClassifier(static exception => exception is SocketException));

        var result = await service.GetList(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListStateCodec.Stale, item.State);
        Assert.Equal(DaemonListReasonCodec.StaleSession, item.Reason);
        Assert.Equal("Daemon session exists but daemon is not reachable.", item.Message);
        Assert.Equal(2200, item.ProcessId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeFailsUnexpectedly_ReturnsProbeFailedItem ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = CreateSession("fp-current", "endpoint-failed", 2300);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new InvalidOperationException("boom"))),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetList(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListStateCodec.Error, item.State);
        Assert.Equal(DaemonListReasonCodec.ProbeFailed, item.Reason);
        Assert.Equal("boom", item.Message);
        Assert.Equal(2300, item.ProcessId);
    }

    private static DaemonListQueryService CreateSingleWorktreeService (
        ResolvedUnityProjectContext currentProject,
        DaemonSessionReadResult sessionReadResult,
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier reachabilityClassifier)
    {
        return new DaemonListQueryService(
            new StubGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
                CurrentWorktreeRoot: currentProject.RepositoryRoot,
                ProjectRelativePath: currentProject.UnityProjectRoot == currentProject.RepositoryRoot ? "." : "UnityProject",
                Worktrees:
                [
                    new GitWorktreeInfo(currentProject.RepositoryRoot, "abcdef01", "refs/heads/main"),
                ]))),
            new StubUnityProjectResolver(currentProject),
            new StubDaemonSessionStore((_, _) => sessionReadResult),
            daemonPingClient,
            reachabilityClassifier);
    }

    private static ResolvedUnityProjectContext CreateUnityProject (
        string worktreeRoot,
        string projectRelativePath,
        string fingerprint)
    {
        var normalizedWorktreeRoot = Path.GetFullPath(worktreeRoot);
        var normalizedProjectRoot = projectRelativePath == "."
            ? normalizedWorktreeRoot
            : Path.Combine(normalizedWorktreeRoot, projectRelativePath);
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: normalizedProjectRoot,
            RepositoryRoot: normalizedWorktreeRoot,
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession (
        string projectFingerprint,
        string endpointAddress,
        int processId)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "secret-token",
            ProjectFingerprint: projectFingerprint,
            IssuedAtUtc: new DateTimeOffset(2026, 03, 09, 12, 0, 0, TimeSpan.Zero),
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindCli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: endpointAddress,
            ProcessId: processId);
    }

    private sealed class StubGitWorktreeQueryService : IGitWorktreeQueryService
    {
        private readonly GitWorktreeQueryResult result;

        public StubGitWorktreeQueryService (GitWorktreeQueryResult result)
        {
            this.result = result;
        }

        public List<string> QueryPaths { get; } = new();

        public ValueTask<GitWorktreeQueryResult> GetWorktreeInfo (
            string path,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            QueryPaths.Add(path);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubUnityProjectResolver : IUnityProjectResolver
    {
        private readonly Dictionary<string, ResolvedUnityProjectContext> contextsByPath;

        public StubUnityProjectResolver (params ResolvedUnityProjectContext[] contexts)
        {
            contextsByPath = contexts.ToDictionary(
                static context => context.UnityProjectRoot,
                StringComparer.Ordinal);
        }

        public UnityProjectResolutionResult Resolve (string? projectPath)
        {
            if (projectPath != null && contextsByPath.TryGetValue(Path.GetFullPath(projectPath), out var context))
            {
                return UnityProjectResolutionResult.Success(context);
            }

            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject path does not exist: {projectPath}"));
        }
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        private readonly Func<string, string, DaemonSessionReadResult> read;

        public StubDaemonSessionStore (Func<string, string, DaemonSessionReadResult> read)
        {
            this.read = read;
        }

        public ValueTask<DaemonSessionReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(read(storageRoot, projectFingerprint));
        }

        public ValueTask<DaemonSessionStoreOperationResult> Write (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonSessionStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        private readonly Func<ResolvedUnityProjectContext, TimeSpan, string?, CancellationToken, ValueTask> ping;

        public StubDaemonPingClient (Func<ResolvedUnityProjectContext, TimeSpan, string?, CancellationToken, ValueTask> ping)
        {
            this.ping = ping;
        }

        public ValueTask Ping (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            CancellationToken cancellationToken = default)
        {
            return ping(unityProject, timeout, sessionToken, cancellationToken);
        }
    }

    private sealed class StubDaemonReachabilityClassifier : IDaemonReachabilityClassifier
    {
        private readonly Func<Exception, bool> isNotRunning;

        public StubDaemonReachabilityClassifier (Func<Exception, bool> isNotRunning)
        {
            this.isNotRunning = isNotRunning;
        }

        public bool IsNotRunning (Exception exception)
        {
            return isNotRunning(exception);
        }
    }
}