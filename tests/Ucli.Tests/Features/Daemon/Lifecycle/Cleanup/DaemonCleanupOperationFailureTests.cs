using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
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

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-probe-failure"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

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
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-lock-context");
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

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-lock-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("lifecycle lock", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
