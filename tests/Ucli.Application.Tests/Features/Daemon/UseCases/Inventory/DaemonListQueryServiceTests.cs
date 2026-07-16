using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Git;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonListQueryServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonListQueryServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WithMixedWorktrees_ReturnsSortedRunningItemsAndSkipsMissingSessions ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var worktreeA = CreateUnityProject("/repo/wt-a", "UnityProject", "fp-a");
        var worktreeB = CreateUnityProject("/repo/wt-b", "UnityProject", "fp-b");
        var gitWorktreeQueryService = new RecordingGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
            CurrentWorktreeRoot: currentProject.RepositoryRoot,
            ProjectRelativePath: "UnityProject",
            Worktrees:
            [
                new GitWorktreeInfo("/repo/wt-b", "bbbbbbbb", "refs/heads/feature/worktree-b"),
                new GitWorktreeInfo("/repo/wt-missing", "mmmmmmmm", "refs/heads/missing"),
                new GitWorktreeInfo("/repo/wt-a", "aaaaaaaa", null),
                new GitWorktreeInfo("/repo/wt-current", "cccccccc", "refs/heads/main"),
            ])));
        var unityProjectResolver = RecordingUnityProjectResolver.FromContexts(
            currentProject,
            worktreeA,
            worktreeB);
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing())
        {
            ReadAsyncHandler = (_, projectFingerprint, _) => ValueTask.FromResult(projectFingerprint switch
            {
                _ when projectFingerprint == currentProject.ProjectFingerprint => DaemonSessionReadResult.Missing(),
                _ when projectFingerprint == worktreeA.ProjectFingerprint => DaemonSessionReadResultTestFactory.Found(DaemonSessionTestFactory.Create(
                    projectFingerprint: projectFingerprint,
                    endpointAddress: "endpoint-a",
                    processId: 1001)),
                _ when projectFingerprint == worktreeB.ProjectFingerprint => DaemonSessionReadResultTestFactory.Found(DaemonSessionTestFactory.Create(
                    projectFingerprint: projectFingerprint,
                    endpointAddress: "endpoint-b",
                    processId: 1002)),
                _ => throw new InvalidOperationException($"Unexpected fingerprint: {projectFingerprint}"),
            }),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var pingClient = CreateDefaultPingClient();
        var service = CreateService(
            gitWorktreeQueryService,
            unityProjectResolver,
            sessionStore,
            diagnosisStore,
            pingClient,
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(2500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        DaemonListExecutionOutputAssert.CompleteRunningWorktreesSortedByPath(
            output,
            expectedTimeoutMilliseconds: 2500,
            expectedProjectRelativePath: "UnityProject",
            new DaemonListExecutionOutputAssert.RunningWorktreeItem(
                WorktreePath: "/repo/wt-a",
                BranchRef: null,
                Head: "aaaaaaaa",
                ProjectPath: worktreeA.UnityProjectRoot,
                ProjectFingerprint: ProjectFingerprintTestFactory.Create("fp-a"),
                ProcessId: 1001,
                EditorMode: DaemonEditorMode.Batchmode,
                OwnerKind: DaemonSessionOwnerKind.Cli,
                CanShutdownProcess: true,
                EndpointAddress: "endpoint-a"),
            new DaemonListExecutionOutputAssert.RunningWorktreeItem(
                WorktreePath: "/repo/wt-b",
                BranchRef: "refs/heads/feature/worktree-b",
                Head: "bbbbbbbb",
                ProjectPath: worktreeB.UnityProjectRoot,
                ProjectFingerprint: ProjectFingerprintTestFactory.Create("fp-b"),
                ProcessId: 1002,
                EditorMode: DaemonEditorMode.Batchmode,
                OwnerKind: DaemonSessionOwnerKind.Cli,
                CanShutdownProcess: true,
                EndpointAddress: "endpoint-b"));

        Assert.Equal([currentProject.UnityProjectRoot], gitWorktreeQueryService.QueryPaths);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenGuiSessionIsRunning_ReturnsOwnerShutdownAndLifecycleFields ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: ProjectFingerprintTestFactory.Create("fp-current"),
            endpointAddress: "endpoint-gui",
            processId: 3101,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false);
        var pingResponse = new IpcUnityEditorObservation(
            serverVersion: "0.0.2",
            unityVersion: "6000.1.4f1",
            projectFingerprint: currentProject.ProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Gui,
                lifecycleState: IpcEditorLifecycleState.PlayMode,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(3, 5, 0, 1),
                playMode: new IpcPlayModeSnapshot(
                    IpcPlayModeState.Playing,
                    IpcPlayModeTransition.None,
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true)),
            observedAtUtc: DateTimeOffset.UnixEpoch,
            actionRequired: null,
            primaryDiagnostic: null);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResultTestFactory.Found(session),
            new RecordingDaemonDiagnosisStore(),
            new RecordingDaemonPingInfoClient(pingResponse),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(3000), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Running, item.State);
        Assert.Equal(DaemonEditorMode.Gui, item.EditorMode);
        Assert.Equal(DaemonSessionOwnerKind.User, item.OwnerKind);
        Assert.False(item.CanShutdownProcess);
        Assert.Equal(IpcEditorLifecycleState.PlayMode, item.LifecycleState);
        Assert.Equal(IpcEditorBlockingReason.PlayMode, item.BlockingReason);
        Assert.False(item.CanAcceptExecutionRequests);
        Assert.Null(item.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenGitWorktreeQueryFails_ReturnsFailure ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var service = CreateService(
            new RecordingGitWorktreeQueryService(GitWorktreeQueryResult.Failure(ExecutionError.InternalError("git failed"))),
            RecordingUnityProjectResolver.FromContexts(currentProject),
            new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing()),
            new RecordingDaemonDiagnosisStore(),
            CreateDefaultPingClient(),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

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
            DaemonSessionReadResultTestFactory.Invalid(),
            new RecordingDaemonDiagnosisStore(),
            CreateDefaultPingClient(),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(3000), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Error, item.State);
        Assert.Equal(DaemonListItemReason.InvalidSession, item.Reason);
        Assert.Null(item.IssuedAtUtc);
        Assert.Null(item.ProcessId);
        Assert.Null(item.EndpointTransportKind);
        Assert.Null(item.EndpointAddress);
        Assert.Null(item.Diagnosis);
    }
}
