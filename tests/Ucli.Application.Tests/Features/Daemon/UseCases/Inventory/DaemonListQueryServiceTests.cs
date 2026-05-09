using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Git;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonListQueryServiceTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

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
        var sessionStore = new StubDaemonSessionStore((_, projectFingerprint) =>
        {
            return projectFingerprint switch
            {
                "fp-current" => DaemonSessionReadResult.Success(null),
                "fp-a" => DaemonSessionReadResult.Success(CreateSession(projectFingerprint, "endpoint-a", 1001)),
                "fp-b" => DaemonSessionReadResult.Success(CreateSession(projectFingerprint, "endpoint-b", 1002)),
                _ => throw new InvalidOperationException($"Unexpected fingerprint: {projectFingerprint}"),
            };
        });
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var pingClient = new StubDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask);
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
                Assert.Equal(DaemonListItemState.Running, item.State);
                Assert.Null(item.Reason);
                Assert.Equal(1001, item.ProcessId);
                Assert.Equal(DaemonEditorModeValues.Batchmode, item.EditorMode);
                Assert.Equal(DaemonSessionOwnerKindValues.Cli, item.OwnerKind);
                Assert.True(item.CanShutdownProcess);
                Assert.Equal("endpoint-a", item.EndpointAddress);
                Assert.Null(item.Diagnosis);
            },
            item =>
            {
                Assert.Equal("/repo/wt-b", item.WorktreePath);
                Assert.Equal("refs/heads/feature/worktree-b", item.BranchRef);
                Assert.Equal("bbbbbbbb", item.Head);
                Assert.Equal(worktreeB.UnityProjectRoot, item.ProjectPath);
                Assert.Equal("fp-b", item.ProjectFingerprint);
                Assert.Equal(DaemonListItemState.Running, item.State);
                Assert.Null(item.Reason);
                Assert.Equal(1002, item.ProcessId);
                Assert.Equal(DaemonEditorModeValues.Batchmode, item.EditorMode);
                Assert.Equal(DaemonSessionOwnerKindValues.Cli, item.OwnerKind);
                Assert.True(item.CanShutdownProcess);
                Assert.Equal("endpoint-b", item.EndpointAddress);
                Assert.Null(item.Diagnosis);
            });

        Assert.Equal([currentProject.UnityProjectRoot], gitWorktreeQueryService.QueryPaths);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenGitWorktreeQueryFails_ReturnsFailure ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var service = CreateService(
            new StubGitWorktreeQueryService(GitWorktreeQueryResult.Failure(ExecutionError.InternalError("git failed"))),
            new StubUnityProjectResolver(currentProject),
            new StubDaemonSessionStore(static (_, _) => DaemonSessionReadResult.Success(null)),
            new StubDaemonDiagnosisStore(),
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask),
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
            DaemonSessionReadResult.Failure(
                ExecutionError.InvalidArgument("Daemon session JSON is invalid."),
                DaemonSessionReadFailureKind.InvalidSession),
            new StubDaemonDiagnosisStore(),
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask),
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenSessionReadOnlyStopsAfterInjectedTimeout_ReturnsPartialSuccess ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var timeProvider = new ManualTimeProvider();
        var sessionReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateService(
            new StubGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
                CurrentWorktreeRoot: currentProject.RepositoryRoot,
                ProjectRelativePath: "UnityProject",
                Worktrees:
                [
                    new GitWorktreeInfo(currentProject.RepositoryRoot, "abcdef01", "refs/heads/main"),
                ]))),
            new StubUnityProjectResolver(currentProject),
            new StubDaemonSessionStore(async (_, _, cancellationToken) =>
            {
                sessionReadStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new System.Diagnostics.UnreachableException();
            }),
            new StubDaemonDiagnosisStore(),
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask),
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
            new StubGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
                CurrentWorktreeRoot: currentProject.RepositoryRoot,
                ProjectRelativePath: "UnityProject",
                Worktrees:
                [
                    new GitWorktreeInfo(currentProject.RepositoryRoot, "abcdef01", "refs/heads/main"),
                ]))),
            new StubUnityProjectResolver(currentProject),
            new StubDaemonSessionStore(async (_, _, cancellationToken) =>
            {
                sessionReadStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new System.Diagnostics.UnreachableException();
            }),
            new StubDaemonDiagnosisStore(),
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.CompletedTask),
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
        var session = CreateSession("fp-current", "endpoint-timeout", 2100);
        var timeProvider = new ManualTimeProvider();
        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new StubDaemonDiagnosisStore(),
            new StubDaemonPingClient(async (_, _, _, cancellationToken) =>
            {
                probeStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
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
        var session = CreateSession("fp-current", "endpoint-timeout", 2100);
        var timeProvider = new ManualTimeProvider();
        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new StubDaemonDiagnosisStore(),
            new StubDaemonPingClient(async (_, _, _, cancellationToken) =>
            {
                probeStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
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
    public async Task List_WhenProbeTimesOut_ReturnsProbeTimeoutItem ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = CreateSession("fp-current", "endpoint-timeout", 2100);
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            new StubDaemonDiagnosisStore(),
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new TimeoutException("probe timed out"))),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Error, item.State);
        Assert.Equal(DaemonListItemReason.ProbeTimeout, item.Reason);
        Assert.Equal(2100, item.ProcessId);
        Assert.Equal("endpoint-timeout", item.EndpointAddress);
        Assert.Null(item.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenSharedDeadlineExpiresDuringProbe_ReturnsPartialSuccess ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var worktreeA = CreateUnityProject("/repo/wt-a", "UnityProject", "fp-a");
        var worktreeB = CreateUnityProject("/repo/wt-b", "UnityProject", "fp-b");
        var timeProvider = new ManualTimeProvider();
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
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateService(
            gitWorktreeQueryService,
            unityProjectResolver,
            sessionStore,
            diagnosisStore,
            new StubDaemonPingClient(async (unityProject, _, _, cancellationToken) =>
            {
                if (unityProject.ProjectFingerprint == "fp-b")
                {
                    timeProvider.Advance(TimeSpan.FromMilliseconds(50));
                    throw new TimeoutException("probe timed out");
                }
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
    public async Task List_WhenProbeClassifiesNotRunningAndPersistedDiagnosisMatches_ReturnsStaleItemWithDiagnosis ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = CreateSession("fp-current", "endpoint-stale", 2200);
        var diagnosis = CreateDiagnosis(session, DaemonDiagnosisReasonValues.ShutdownRequested);
        var diagnosisStore = new StubDaemonDiagnosisStore
        {
            OnRead = (_, _) => DaemonDiagnosisReadResult.Success(diagnosis),
        };
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            diagnosisStore,
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            new StubDaemonReachabilityClassifier(static exception => exception is SocketException));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Stale, item.State);
        Assert.Equal(DaemonListItemReason.StaleSession, item.Reason);
        Assert.Equal(2200, item.ProcessId);
        Assert.NotNull(item.Diagnosis);
        Assert.Equal(diagnosis.Reason, item.Diagnosis!.Reason);
        Assert.Equal(diagnosis.Message, item.Diagnosis.Message);
        Assert.Equal(diagnosis.ReportedBy, item.Diagnosis.ReportedBy);
        Assert.Equal(diagnosis.IsInferred, item.Diagnosis.IsInferred);
        Assert.Equal(diagnosis.UpdatedAtUtc, item.Diagnosis.UpdatedAtUtc);
        Assert.Equal(diagnosis.ProcessId, item.Diagnosis.ProcessId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenProbeClassifiesNotRunningWithoutPersistedDiagnosis_ReturnsExternalTerminationDiagnosis ()
    {
        var currentProject = CreateUnityProject("/repo/wt-current", "UnityProject", "fp-current");
        var session = CreateSession("fp-current", "endpoint-stale", int.MaxValue);
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var service = CreateSingleWorktreeService(
            currentProject,
            DaemonSessionReadResult.Success(session),
            diagnosisStore,
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new SocketException((int)SocketError.ConnectionRefused))),
            new StubDaemonReachabilityClassifier(static exception => exception is SocketException));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Stale, item.State);
        Assert.Equal(DaemonListItemReason.StaleSession, item.Reason);
        Assert.NotNull(item.Diagnosis);
        Assert.Equal(DaemonDiagnosisReasonValues.ExternalTerminationSuspected, item.Diagnosis!.Reason);
        Assert.Equal(DaemonDiagnosisReportedByValues.Cli, item.Diagnosis.ReportedBy);
        Assert.True(item.Diagnosis.IsInferred);
        Assert.Equal(session.ProcessId, item.Diagnosis.ProcessId);
        Assert.Equal(1, diagnosisStore.WriteCallCount);
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
            new StubDaemonDiagnosisStore(),
            new StubDaemonPingClient(static (_, _, _, _) => ValueTask.FromException(new InvalidOperationException("boom"))),
            new StubDaemonReachabilityClassifier(static _ => false));

        var result = await service.GetListAsync(currentProject, TimeSpan.FromMilliseconds(1200), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonListExecutionOutput>(result.Output);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        var item = Assert.Single(output.Items);
        Assert.Equal(DaemonListItemState.Error, item.State);
        Assert.Equal(DaemonListItemReason.ProbeFailed, item.Reason);
        Assert.Equal(2300, item.ProcessId);
        Assert.Null(item.Diagnosis);
    }

    private static DaemonListQueryService CreateSingleWorktreeService (
        ResolvedUnityProjectContext currentProject,
        DaemonSessionReadResult sessionReadResult,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        TimeProvider? timeProvider = null)
    {
        return CreateService(
            new StubGitWorktreeQueryService(GitWorktreeQueryResult.Success(new GitWorktreeQueryOutput(
                CurrentWorktreeRoot: currentProject.RepositoryRoot,
                ProjectRelativePath: currentProject.UnityProjectRoot == currentProject.RepositoryRoot ? "." : "UnityProject",
                Worktrees:
                [
                    new GitWorktreeInfo(currentProject.RepositoryRoot, "abcdef01", "refs/heads/main"),
                ]))),
            new StubUnityProjectResolver(currentProject),
            new StubDaemonSessionStore((_, _) => sessionReadResult),
            daemonDiagnosisStore,
            daemonPingClient,
            reachabilityClassifier,
            timeProvider);
    }

    private static DaemonListQueryService CreateService (
        IGitWorktreeQueryService gitWorktreeQueryService,
        IUnityProjectResolver unityProjectResolver,
        IDaemonSessionStore daemonSessionStore,
        IDaemonDiagnosisStore daemonDiagnosisStore,
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier daemonReachabilityClassifier,
        TimeProvider? timeProvider = null)
    {
        return new DaemonListQueryService(
            gitWorktreeQueryService,
            unityProjectResolver,
            daemonSessionStore,
            daemonDiagnosisStore,
            daemonPingClient,
            daemonReachabilityClassifier,
            CreateDiagnosisResolver(daemonDiagnosisStore),
            new DaemonDiagnosisOutputMapper(),
            new StubWorktreeProjectPathResolver(),
            timeProvider);
    }

    private static DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver CreateDiagnosisResolver (IDaemonDiagnosisStore daemonDiagnosisStore)
    {
        return new DaemonServiceTestContext.StubDaemonSessionDiagnosisResolver
        {
            Handler = async (unityProject, session, persistedDiagnosis, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (persistedDiagnosis is not null
                    && persistedDiagnosis.SessionIssuedAtUtc == session.IssuedAtUtc)
                {
                    return persistedDiagnosis;
                }

                if (session.ProcessId is not int processId)
                {
                    return null;
                }

                var diagnosis = new DaemonDiagnosis(
                    Reason: DaemonDiagnosisReasonValues.ExternalTerminationSuspected,
                    Message: "Daemon process is no longer alive and no persisted diagnosis matched the current session.",
                    ReportedBy: DaemonDiagnosisReportedByValues.Cli,
                    IsInferred: true,
                    UpdatedAtUtc: DateTimeOffset.UtcNow,
                    ProcessId: processId,
                    EditorInstancePath: null,
                    SessionIssuedAtUtc: session.IssuedAtUtc);

                await daemonDiagnosisStore.WriteAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        diagnosis,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                return diagnosis;
            },
        };
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
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: endpointAddress,
            ProcessId: processId,

            OwnerProcessId: 9876);
    }

    private static DaemonDiagnosis CreateDiagnosis (
        DaemonSession session,
        string reason)
    {
        return new DaemonDiagnosis(
            Reason: reason,
            Message: $"diagnosis:{reason}",
            ReportedBy: DaemonDiagnosisReportedByValues.Unity,
            IsInferred: false,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 12, 1, 0, TimeSpan.Zero),
            ProcessId: session.ProcessId,
            EditorInstancePath: null,
            SessionIssuedAtUtc: session.IssuedAtUtc);
    }

    private sealed class StubGitWorktreeQueryService : IGitWorktreeQueryService
    {
        private readonly GitWorktreeQueryResult result;

        public StubGitWorktreeQueryService (GitWorktreeQueryResult result)
        {
            this.result = result;
        }

        public List<string> QueryPaths { get; } = new();

        public ValueTask<GitWorktreeQueryResult> GetWorktreeInfoAsync (
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

        public UnityProjectResolutionResult Resolve (ProjectPathCandidate projectPathCandidate)
        {
            ArgumentNullException.ThrowIfNull(projectPathCandidate);

            if (contextsByPath.TryGetValue(Path.GetFullPath(projectPathCandidate.Path), out var context))
            {
                return UnityProjectResolutionResult.Success(context);
            }

            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject path does not exist: {projectPathCandidate.Path}",
                ProjectContextErrorCodes.ProjectPathNotFound));
        }
    }

    private sealed class StubWorktreeProjectPathResolver : IWorktreeProjectPathResolver
    {
        public string ResolveCandidateProjectPath (
            string worktreePath,
            string projectRelativePath)
        {
            return string.Equals(projectRelativePath, ".", StringComparison.Ordinal)
                ? worktreePath
                : Path.Combine(worktreePath, projectRelativePath);
        }
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        private readonly Func<string, string, CancellationToken, ValueTask<DaemonSessionReadResult>> read;

        public StubDaemonSessionStore (Func<string, string, DaemonSessionReadResult> read)
        {
            this.read = (storageRoot, projectFingerprint, _) =>
                ValueTask.FromResult(read(storageRoot, projectFingerprint));
        }

        public StubDaemonSessionStore (Func<string, string, CancellationToken, ValueTask<DaemonSessionReadResult>> read)
        {
            this.read = read;
        }

        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return read(storageRoot, projectFingerprint, cancellationToken);
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public Func<string, string, DaemonDiagnosisReadResult> OnRead { get; set; } = static (_, _) => DaemonDiagnosisReadResult.Success(null);

        public DaemonDiagnosisStoreOperationResult WriteResult { get; set; } = DaemonDiagnosisStoreOperationResult.Success();

        public int WriteCallCount { get; private set; }

        public DaemonDiagnosis? LastDiagnosis { get; private set; }

        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(OnRead(storageRoot, projectFingerprint));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            WriteCallCount++;
            LastDiagnosis = diagnosis;
            return ValueTask.FromResult(WriteResult);
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
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

        public ValueTask PingAsync (
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
