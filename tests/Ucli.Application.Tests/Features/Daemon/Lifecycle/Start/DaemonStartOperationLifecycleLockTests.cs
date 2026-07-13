using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStartOperationTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartOperationLifecycleLockTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenLifecycleLockAcquireTimesOut_ReturnsTimeoutFailure ()
    {
        var lockProvider = new StubProjectLifecycleLockProvider
        {
            ThrowTimeoutOnAcquire = true,
        };
        var operation = CreateOperation(
            daemonSessionStore: new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null)),
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: new RecordingDaemonLaunchService(),
            lifecycleLockProvider: lockProvider);

        var result = await operation.StartAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-lock-timeout"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("lifecycle lock", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenWorkflowBegins_AcquiresLifecycleLockForUnityProjectRoot ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-lock-context");
        var lockProvider = new StubProjectLifecycleLockProvider();
        var operation = CreateOperation(
            daemonSessionStore: new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null)),
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: new RecordingDaemonLaunchService
            {
                NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 2026, projectFingerprint: context.ProjectFingerprint), IpcUnityEditorObservationTestFactory.Create()),
            },
            lifecycleLockProvider: lockProvider);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        ProjectLifecycleLockProviderAssert.LifecycleLockAcquiredFor(lockProvider, context);
    }
}
