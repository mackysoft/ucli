using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStopOperationTestSupport;
using static MackySoft.Ucli.Application.Tests.DaemonCleanupInvocationAssert;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStopOperationLifecycleTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenWorkflowBegins_AcquiresLifecycleLockForUnityProjectRoot ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-lock-context");
        var lockProvider = new StubProjectLifecycleLockProvider();
        var operation = CreateOperation(
            lifecycleLockProvider: lockProvider,
            sessionStore: new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null)));

        var result = await operation.StopAsync(context, DefaultTimeout, CancellationToken.None);

        Assert.Equal(DaemonStopStatus.NotRunning, result.Status);
        ProjectLifecycleLockProviderAssert.LifecycleLockAcquiredFor(lockProvider, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenLifecycleLockAcquireTimesOut_ReturnsTimeoutFailure ()
    {
        var lockProvider = new StubProjectLifecycleLockProvider
        {
            ThrowTimeoutOnAcquire = true,
        };
        var operation = CreateOperation(
            lifecycleLockProvider: lockProvider,
            sessionStore: new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null)));

        var result = await operation.StopAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-lock-timeout"),
            DefaultTimeout,
            CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("lifecycle lock", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stop_WhenProcessTerminationBudgetIsExhausted_StillAttemptsFinalizationWithFallbackTimeout (bool endpointAlreadyNotRunning)
    {
        var timeProvider = new ManualTimeProvider();
        var session = DaemonSessionTestFactory.Create(processId: 789);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-stop-timeout-finalization");
        var shutdownClient = new RecordingDaemonShutdownClient
        {
            Delay = TimeSpan.FromMilliseconds(80),
            NextResult = endpointAlreadyNotRunning
                ? DaemonShutdownAttemptResult.NotRunning()
                : DaemonShutdownAttemptResult.Success(),
            TimeProvider = timeProvider,
        };
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            NextResult = DaemonSessionStoreOperationResult.Success(),
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = CreateOperation(
            sessionStore: CreateSessionStore(session),
            shutdownClient: shutdownClient,
            processTerminationService: processTerminationService,
            artifactCleaner: artifactCleaner,
            timeProvider: timeProvider);

        var result = await operation.StopAsync(
            context,
            TimeSpan.FromMilliseconds(20),
            CancellationToken.None);

        Assert.Equal(DaemonStopStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        DaemonShutdownClientAssert.EndpointShutdownAttempted(shutdownClient, context, session);
        AssertProcessTerminationAttempted(
            processTerminationService,
            789,
            session.ProcessStartedAtUtc,
            DaemonTimeouts.StopCompensationTimeout);
        AssertSessionArtifactsInvalidated(artifactCleaner, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenArtifactCleanupIgnoresDeadline_ReturnsTimeoutAndBlocksSuccessorUntilCleanupQuiesces ()
    {
        var timeProvider = new ManualTimeProvider();
        var compensationOperationOwner = new DaemonCompensationOperationOwner();
        var lifecycleLeases = new List<RecordingAsyncDisposable>();
        var lifecycleLockProvider = new StubProjectLifecycleLockProvider(
            (_, _, _) =>
            {
                var lifecycleLease = new RecordingAsyncDisposable();
                lifecycleLeases.Add(lifecycleLease);
                return lifecycleLease;
            });
        var session = DaemonSessionTestFactory.Create(processId: 790);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            "fingerprint-stop-owned-cleanup");
        var cleanupStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource<DaemonArtifactCleanupResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            CleanupHandler = (_, _) =>
            {
                cleanupStarted.TrySetResult();
                return releaseCleanup.Task;
            },
        };
        var sessionStore = CreateSessionStore(session);
        var operation = CreateOperation(
            lifecycleLockProvider: lifecycleLockProvider,
            sessionStore: sessionStore,
            shutdownClient: new RecordingDaemonShutdownClient
            {
                NextResult = DaemonShutdownAttemptResult.Success(),
            },
            processTerminationService: new RecordingDaemonProcessTerminationService(),
            artifactCleaner: artifactCleaner,
            compensationOperationOwner: compensationOperationOwner,
            timeProvider: timeProvider);

        var firstStopTask = operation.StopAsync(
                context,
                TimeSpan.FromMilliseconds(500),
                CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(
            cleanupStarted.Task,
            "Stop artifact cleanup start",
            TimeSpan.FromSeconds(5));

        timeProvider.Advance(DaemonTimeouts.StopCompensationTimeout);
        var firstResult = await TestAwaiter.WaitAsync(
            firstStopTask,
            "Stop compensation deadline result",
            TimeSpan.FromSeconds(5));
        Assert.Equal(DaemonStopStatus.Failed, firstResult.Status);
        Assert.Equal(ExecutionErrorKind.Timeout, firstResult.Error!.Kind);
        Assert.Equal(0, Assert.Single(lifecycleLeases).DisposeCount);

        var secondStopTask = operation.StopAsync(
                context,
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None)
            .AsTask();
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var secondResult = await TestAwaiter.WaitAsync(
            secondStopTask,
            "Successor stop admission result",
            TimeSpan.FromSeconds(5));
        Assert.Equal(DaemonStopStatus.Failed, secondResult.Status);
        Assert.Equal(ExecutionErrorKind.Timeout, secondResult.Error!.Kind);
        Assert.Single(sessionStore.ReadInvocations);
        Assert.Equal(1, lifecycleLeases[1].DisposeCount);
        Assert.Equal(0, lifecycleLeases[0].DisposeCount);

        releaseCleanup.TrySetResult(DaemonArtifactCleanupResult.Success());
        var quiescenceError = await TestAwaiter.WaitAsync(
            compensationOperationOwner.WaitForQuiescenceAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                    CancellationToken.None,
                    "Timed out waiting for stop compensation cleanup.")
                .AsTask(),
            "Stop compensation quiescence",
            TimeSpan.FromSeconds(5));
        Assert.Null(quiescenceError);
        Assert.Equal(1, lifecycleLeases[0].DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenCallerCancelsAfterShutdown_ReturnsCancellationAndKeepsCompensationAndLeaseOwned ()
    {
        var timeProvider = new ManualTimeProvider();
        var compensationOperationOwner = new DaemonCompensationOperationOwner();
        var lifecycleLease = new RecordingAsyncDisposable();
        var session = DaemonSessionTestFactory.Create(processId: 791);
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(
            "fingerprint-stop-canceled-compensation");
        using var cancellationTokenSource = new CancellationTokenSource();
        var terminationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTermination = new TaskCompletionSource<DaemonSessionStoreOperationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var processTerminationService = new RecordingDaemonProcessTerminationService
        {
            Handler = (_, _, ownedCancellationToken) =>
            {
                Assert.False(ownedCancellationToken.IsCancellationRequested);
                terminationStarted.TrySetResult();
                return new ValueTask<DaemonSessionStoreOperationResult>(releaseTermination.Task);
            },
        };
        var operation = CreateOperation(
            lifecycleLockProvider: new StubProjectLifecycleLockProvider(
                (_, _, _) => lifecycleLease),
            sessionStore: CreateSessionStore(session),
            shutdownClient: new RecordingDaemonShutdownClient
            {
                NextResult = DaemonShutdownAttemptResult.Success(),
                OnSend = cancellationTokenSource.Cancel,
            },
            processTerminationService: processTerminationService,
            artifactCleaner: new RecordingDaemonArtifactCleaner(),
            compensationOperationOwner: compensationOperationOwner,
            timeProvider: timeProvider);

        var stopTask = operation.StopAsync(
                context,
                TimeSpan.FromSeconds(1),
                cancellationTokenSource.Token)
            .AsTask();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TestAwaiter.WaitAsync(
            stopTask,
            "Caller-canceled stop result",
            TimeSpan.FromSeconds(5)));
        await TestAwaiter.WaitAsync(
            terminationStarted.Task,
            "Caller-canceled stop compensation start",
            TimeSpan.FromSeconds(5));
        Assert.Equal(0, lifecycleLease.DisposeCount);

        releaseTermination.TrySetResult(DaemonSessionStoreOperationResult.Success());
        var quiescenceError = await TestAwaiter.WaitAsync(
            compensationOperationOwner.WaitForQuiescenceAsync(
                    context,
                    ExecutionDeadline.Start(TimeSpan.FromSeconds(1), timeProvider),
                    CancellationToken.None,
                    "Timed out waiting for canceled stop compensation.")
                .AsTask(),
            "Caller-canceled stop compensation quiescence",
            TimeSpan.FromSeconds(5));
        Assert.Null(quiescenceError);
        Assert.Equal(1, lifecycleLease.DisposeCount);
    }

    private sealed class RecordingAsyncDisposable : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync ()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
