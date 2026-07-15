using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonCleanupOperationFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenProbeFailsUnexpectedly_ReturnsFailureWithoutCleanup ()
    {
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(DaemonSessionTestFactory.Create(processId: 2011)),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateFailingPingClient(
                new InvalidDataException("invalid frame")),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-cleanup-probe-failure")), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        var error = DaemonCleanupOperationAssert.FailedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            ExecutionErrorKind.InternalError);
        Assert.Contains("Failed to probe daemon cleanup reachability", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenWorkflowBegins_AcquiresLifecycleLockForUnityProjectRoot ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-cleanup-lock-context"));
        var lockProvider = new StubProjectLifecycleLockProvider();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            lifecycleLockProvider: lockProvider,
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Missing(),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateNotRunningPingClient());

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        ProjectLifecycleLockProviderAssert.AcquiredOnceFor(lockProvider, context.UnityProjectRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenLifecycleLockAcquireTimesOut_ReturnsTimeoutFailure ()
    {
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            TimeProvider.System,
            lifecycleLockProvider: new StubProjectLifecycleLockProvider(throwTimeout: true));

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-cleanup-lock-timeout")), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("lifecycle lock", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenInitialSessionReadIgnoresCancellation_ReturnsTimeoutWithoutLateSideEffects ()
    {
        var readStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var readCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var daemonSessionStore = new RecordingDaemonSessionStore
        {
            ReadAsyncHandler = async (_, _, _) =>
            {
                readStarted.TrySetResult();
                await releaseRead.Task.ConfigureAwait(false);
                readCompleted.TrySetResult();
                return DaemonSessionReadResult.Missing();
            },
        };
        var daemonPingClient = DaemonCleanupOperationTestSupport.CreateSuccessfulPingClient();
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var timeProvider = new ManualTimeProvider();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            timeProvider,
            daemonSessionStore: daemonSessionStore,
            daemonPingClient: daemonPingClient,
            artifactCleaner: artifactCleaner);
        var cleanupTask = operation.CleanupAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-cleanup-read-timeout")),
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None)
            .AsTask();

        try
        {
            await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(100));
            timeProvider.Advance(TimeSpan.FromMilliseconds(100));

            var result = await cleanupTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
            Assert.Empty(daemonPingClient.Invocations);
            Assert.Empty(artifactCleaner.Invocations);

            releaseRead.TrySetResult();
            await readCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Empty(daemonPingClient.Invocations);
            Assert.Empty(artifactCleaner.Invocations);
        }
        finally
        {
            releaseRead.TrySetResult();
        }
    }
}
