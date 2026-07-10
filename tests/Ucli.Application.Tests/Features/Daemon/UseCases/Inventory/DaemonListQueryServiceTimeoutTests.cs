using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
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
    public async Task List_WhenSessionReadIgnoresCancellation_ReturnsAtSharedDeadline ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var timeProvider = new ManualTimeProvider();
        var sessionReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionReadCompletion = new TaskCompletionSource<DaemonSessionReadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                ReadAsyncHandler = (_, _, _) =>
                {
                    sessionReadStarted.TrySetResult();
                    return new ValueTask<DaemonSessionReadResult>(sessionReadCompletion.Task);
                },
            },
            new RecordingDaemonDiagnosisStore(),
            CreateDefaultPingClient(),
            new StubDaemonReachabilityClassifier(static _ => false),
            timeProvider);

        var resultTask = service.GetListAsync(
                currentProject,
                TimeSpan.FromMilliseconds(150),
                CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(sessionReadStarted.Task, "Non-cooperative daemon list session read start", SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        try
        {
            var result = await TestAwaiter.WaitAsync(
                resultTask,
                "Non-cooperative daemon list session read deadline",
                SignalWaitTimeout);

            Assert.True(result.IsSuccess);
            Assert.False(result.Output!.IsComplete);
            Assert.Equal(DaemonListCompletionReason.Timeout, result.Output.CompletionReason);
            Assert.Empty(result.Output.Items);
        }
        finally
        {
            sessionReadCompletion.TrySetResult(DaemonSessionReadResult.Success(null));
        }
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenLifecycleReadStopsAfterUnreachableProbe_ReturnsPartialSuccess ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: currentProject.ProjectFingerprint,
            endpointAddress: "endpoint-unreachable",
            processId: 2501);
        var timeProvider = new ManualTimeProvider();
        var lifecycleStore = new BlockingDaemonLifecycleStore();
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new RecordingDaemonDiagnosisStore(),
            CreateThrowingPingClient(new SocketException((int)SocketError.ConnectionRefused)),
            new StubDaemonReachabilityClassifier(static _ => true),
            timeProvider,
            daemonLifecycleStore: lifecycleStore);

        var resultTask = service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(150), CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(lifecycleStore.ReadStarted, "Daemon lifecycle read start", SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Daemon lifecycle read timeout result", SignalWaitTimeout);

        AssertSingleWorktreeTimeout(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenDiagnosisReadStopsAfterUnreachableProbe_ReturnsPartialSuccess ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: currentProject.ProjectFingerprint,
            endpointAddress: "endpoint-unreachable",
            processId: 2502);
        var timeProvider = new ManualTimeProvider();
        var diagnosisStore = new BlockingDaemonDiagnosisStore();
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            diagnosisStore,
            CreateThrowingPingClient(new SocketException((int)SocketError.ConnectionRefused)),
            new StubDaemonReachabilityClassifier(static _ => true),
            timeProvider);

        var resultTask = service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(150), CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(diagnosisStore.ReadStarted, "Daemon diagnosis read start", SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Daemon diagnosis read timeout result", SignalWaitTimeout);

        AssertSingleWorktreeTimeout(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenDiagnosisResolutionStopsAfterUnreachableProbe_ReturnsPartialSuccess ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: currentProject.ProjectFingerprint,
            endpointAddress: "endpoint-unreachable",
            processId: 2503);
        var timeProvider = new ManualTimeProvider();
        var diagnosisResolutionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosisResolver = new RecordingDaemonSessionDiagnosisResolver
        {
            Handler = async (_, _, _, cancellationToken) =>
            {
                diagnosisResolutionStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new System.Diagnostics.UnreachableException();
            },
        };
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new RecordingDaemonDiagnosisStore(),
            CreateThrowingPingClient(new SocketException((int)SocketError.ConnectionRefused)),
            new StubDaemonReachabilityClassifier(static _ => true),
            timeProvider,
            daemonSessionDiagnosisResolver: diagnosisResolver);

        var resultTask = service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(150), CancellationToken.None).AsTask();
        await TestAwaiter.WaitAsync(diagnosisResolutionStarted.Task, "Daemon diagnosis resolution start", SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Daemon diagnosis resolution timeout result", SignalWaitTimeout);

        AssertSingleWorktreeTimeout(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenCallerCancelsDuringDiagnosisResolution_RethrowsCancellation ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: currentProject.ProjectFingerprint,
            endpointAddress: "endpoint-unreachable",
            processId: 2504);
        var timeProvider = new ManualTimeProvider();
        var diagnosisResolutionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var diagnosisResolver = new RecordingDaemonSessionDiagnosisResolver
        {
            Handler = async (_, _, _, cancellationToken) =>
            {
                diagnosisResolutionStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new System.Diagnostics.UnreachableException();
            },
        };
        using var cancellationTokenSource = new CancellationTokenSource();
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new RecordingDaemonDiagnosisStore(),
            CreateThrowingPingClient(new SocketException((int)SocketError.ConnectionRefused)),
            new StubDaemonReachabilityClassifier(static _ => true),
            timeProvider,
            daemonSessionDiagnosisResolver: diagnosisResolver);

        var resultTask = service.GetListAsync(
                currentProject,
                TimeSpan.FromMilliseconds(150),
                cancellationTokenSource.Token)
            .AsTask();
        await TestAwaiter.WaitAsync(diagnosisResolutionStarted.Task, "Daemon diagnosis resolution start", SignalWaitTimeout);
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await TestAwaiter.WaitAsync(
                    resultTask,
                    "Daemon diagnosis resolution caller cancellation result",
                    SignalWaitTimeout)
                .ConfigureAwait(false));
    }

    private static void AssertSingleWorktreeTimeout (DaemonListExecutionResult result)
    {
        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.False(output.IsComplete);
        Assert.Equal(DaemonListCompletionReason.Timeout, output.CompletionReason);
        Assert.Equal(1, output.RemainingWorktreeCount);
        Assert.Empty(output.Items);
    }

    private sealed class BlockingDaemonLifecycleStore : IDaemonLifecycleStore
    {
        private readonly TaskCompletionSource readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => readStarted.Task;

        public async ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            readStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new System.Diagnostics.UnreachableException();
        }

        public ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class BlockingDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        private readonly TaskCompletionSource readStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReadStarted => readStarted.Task;

        public async ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            readStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            throw new System.Diagnostics.UnreachableException();
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
