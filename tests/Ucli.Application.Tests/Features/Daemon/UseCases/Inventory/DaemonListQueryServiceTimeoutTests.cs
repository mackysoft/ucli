using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Shared.Git;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonListQueryServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonListQueryServiceTimeoutTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenSessionReadOnlyStopsAfterInjectedTimeout_ReturnsPartialSuccess ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var timeProvider = new ManualTimeProvider();
        var sessionReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateService(
            new RecordingGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
                CurrentWorktreeRoot: currentProject.RepositoryRoot,
                ProjectRelativePath: "UnityProject",
                Worktrees:
                [
                    new GitWorktreeInfo(currentProject.RepositoryRoot, "abcdef01", "refs/heads/main"),
                ]))),
            RecordingUnityProjectResolver.FromContexts(currentProject),
            new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null))
            {
                ReadAsyncHandler = async (_, _, cancellationToken) =>
                {
                    sessionReadStarted.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                    throw new System.Diagnostics.UnreachableException();
                },
            },
            new RecordingDaemonDiagnosisStore(),
            CreateDefaultPingClient(),
            new StubDaemonReachabilityClassifier(static _ => false),
            timeProvider);

        var resultTask = service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(150), CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(sessionReadStarted.Task, "Daemon list session read start", SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Daemon list session read timeout result", SignalWaitTimeout);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.False(output.IsComplete);
        Assert.Equal(DaemonListCompletionReason.Timeout, output.CompletionReason);
        Assert.Equal(1, output.RemainingWorktreeCount);
        Assert.Empty(output.Items);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenCallerCancellationRacesSessionReadTimeout_RethrowsCancellation ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var timeProvider = new ManualTimeProvider();
        var sessionReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();
        var service = CreateService(
            new RecordingGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
                CurrentWorktreeRoot: currentProject.RepositoryRoot,
                ProjectRelativePath: "UnityProject",
                Worktrees:
                [
                    new GitWorktreeInfo(currentProject.RepositoryRoot, "abcdef01", "refs/heads/main"),
                ]))),
            RecordingUnityProjectResolver.FromContexts(currentProject),
            new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null))
            {
                ReadAsyncHandler = async (_, _, cancellationToken) =>
                {
                    sessionReadStarted.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                    throw new System.Diagnostics.UnreachableException();
                },
            },
            new RecordingDaemonDiagnosisStore(),
            CreateDefaultPingClient(),
            new StubDaemonReachabilityClassifier(static _ => false),
            timeProvider);

        var resultTask = service.GetListAsync(
                currentProject,
                TimeSpan.FromMilliseconds(150),
                cancellationTokenSource.Token)
            .AsTask();
        await TestAwaiter.WaitAsync(sessionReadStarted.Task, "Daemon list session read start", SignalWaitTimeout);

        cancellationTokenSource.Cancel();
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await TestAwaiter.WaitAsync(
                    resultTask,
                    "Daemon list session read caller cancellation result",
                    SignalWaitTimeout)
                .ConfigureAwait(false));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeOnlyStopsAfterInjectedTimeout_ReturnsPartialSuccess ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fp-current",
            endpointAddress: "endpoint-timeout",
            processId: 2100);
        var timeProvider = new ManualTimeProvider();
        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new RecordingDaemonDiagnosisStore(),
            CreatePingClient(async (_, _, _, _, cancellationToken) =>
            {
                probeStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new System.Diagnostics.UnreachableException();
            }),
            new StubDaemonReachabilityClassifier(static _ => false),
            timeProvider);

        var resultTask = service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(150), CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(probeStarted.Task, "Daemon list probe start", SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Daemon list probe timeout result", SignalWaitTimeout);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.False(output.IsComplete);
        Assert.Equal(DaemonListCompletionReason.Timeout, output.CompletionReason);
        Assert.Equal(1, output.RemainingWorktreeCount);
        Assert.Empty(output.Items);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenCallerCancellationRacesProbeTimeout_RethrowsCancellation ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: "fp-current",
            endpointAddress: "endpoint-timeout",
            processId: 2100);
        var timeProvider = new ManualTimeProvider();
        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new RecordingDaemonDiagnosisStore(),
            CreatePingClient(async (_, _, _, _, cancellationToken) =>
            {
                probeStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new System.Diagnostics.UnreachableException();
            }),
            new StubDaemonReachabilityClassifier(static _ => false),
            timeProvider);

        var resultTask = service.GetListAsync(
                currentProject,
                TimeSpan.FromMilliseconds(150),
                cancellationTokenSource.Token)
            .AsTask();
        await TestAwaiter.WaitAsync(probeStarted.Task, "Daemon list probe start", SignalWaitTimeout);

        cancellationTokenSource.Cancel();
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await TestAwaiter.WaitAsync(
                    resultTask,
                    "Daemon list probe caller cancellation result",
                    SignalWaitTimeout)
                .ConfigureAwait(false));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenSharedDeadlineExpiresDuringProbe_ReturnsPartialSuccess ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var worktreeA = CreateUnityProject("/repo/wt-a", "UnityProject", "fp-a");
        var worktreeB = CreateUnityProject("/repo/wt-b", "UnityProject", "fp-b");
        var timeProvider = new ManualTimeProvider();
        var gitWorktreeQueryService = new RecordingGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
            CurrentWorktreeRoot: currentProject.RepositoryRoot,
            ProjectRelativePath: "UnityProject",
            Worktrees:
            [
                new GitWorktreeInfo("/repo/wt-a", "aaaaaaaa", "refs/heads/a"),
                new GitWorktreeInfo("/repo/wt-b", "bbbbbbbb", "refs/heads/b"),
            ])));
        var unityProjectResolver = RecordingUnityProjectResolver.FromContexts(worktreeA, worktreeB);
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null))
        {
            ReadAsyncHandler = (_, projectFingerprint, _) => ValueTask.FromResult(projectFingerprint switch
            {
                "fp-a" => DaemonSessionReadResult.Success(DaemonSessionTestFactory.Create(
                    projectFingerprint: "fp-a",
                    endpointAddress: "endpoint-a",
                    processId: 2401)),
                "fp-b" => DaemonSessionReadResult.Success(DaemonSessionTestFactory.Create(
                    projectFingerprint: "fp-b",
                    endpointAddress: "endpoint-b",
                    processId: 2402)),
                _ => throw new InvalidOperationException($"Unexpected fingerprint: {projectFingerprint}"),
            }),
        };
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var service = CreateService(
            gitWorktreeQueryService,
            unityProjectResolver,
            sessionStore,
            diagnosisStore,
            CreatePingClient((unityProject, _, _, _, cancellationToken) =>
            {
                if (unityProject.ProjectFingerprint == "fp-b")
                {
                    timeProvider.Advance(TimeSpan.FromMilliseconds(50));
                    return ValueTask.FromException<IpcPingResponse>(new TimeoutException("probe timed out"));
                }

                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(CreatePingResponse(unityProject));
            }),
            new StubDaemonReachabilityClassifier(static _ => false),
            timeProvider);

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.False(output.IsComplete);
        Assert.Equal(DaemonListCompletionReason.Timeout, output.CompletionReason);
        Assert.Equal(1, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal("/repo/wt-a", item.WorktreePath);
        Assert.Equal(DaemonListItemState.Running, item.State);
    }
}
