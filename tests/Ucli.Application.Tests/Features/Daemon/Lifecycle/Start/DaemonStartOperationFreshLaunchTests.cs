using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStartOperationTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartOperationFreshLaunchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenNoRunningSessionAndGuiAttachSucceeds_ReturnsAttachedWithoutFreshLaunch ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-gui-attach");
        var guiSession = DaemonSessionTestFactory.Create(
            processId: 6060,
            projectFingerprint: context.ProjectFingerprint) with
        {
            EditorMode = DaemonEditorMode.Gui,
        };
        var guiAttachService = new RecordingDaemonGuiEditorAttachService
        {
            NextResult = DaemonStartResult.Attached(guiSession, IpcUnityEditorObservationTestFactory.Create(editorMode: DaemonEditorMode.Gui)),
        };
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 7070, projectFingerprint: context.ProjectFingerprint), IpcUnityEditorObservationTestFactory.Create()),
        };
        var operation = CreateOperation(
            daemonSessionStore: new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null)),
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: launchService,
            daemonGuiEditorAttachService: guiAttachService);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Attached, result.Status);
        Assert.Equal(guiSession, result.Session);
        var attachInvocation = DaemonStartOperationInvocationAssert.GuiAttachReturnedWithoutFreshLaunch(
            guiAttachService,
            launchService,
            context);
        Assert.Null(attachInvocation.EditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenGuiAttachReturnsNull_ContinuesToFreshLaunchWithRequestedMode ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-gui-launch");
        var guiAttachService = new RecordingDaemonGuiEditorAttachService();
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 8081, projectFingerprint: context.ProjectFingerprint), IpcUnityEditorObservationTestFactory.Create()),
        };
        var operation = CreateOperation(
            daemonSessionStore: new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null)),
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: launchService,
            daemonGuiEditorAttachService: guiAttachService);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Terminate,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        var attachInvocation = DaemonStartOperationInvocationAssert.GuiAttachAttempted(guiAttachService, context);
        Assert.Equal(DaemonEditorMode.Gui, attachInvocation.EditorMode);
        Assert.Equal(DaemonStartupBlockedProcessPolicy.Terminate, attachInvocation.OnStartupBlocked);
        DaemonStartOperationInvocationAssert.FreshLaunchAttempted(
            launchService,
            context,
            expectedEditorMode: DaemonEditorMode.Gui,
            expectedStartupBlockedPolicy: DaemonStartupBlockedProcessPolicy.Terminate);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSessionDoesNotExist_StartsFreshDaemonWithDefaultBatchmode ()
    {
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null));
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var existingSessionGateService = new RecordingDaemonExistingSessionGateService();
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 9001), IpcUnityEditorObservationTestFactory.Create()),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-no-session"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        DaemonStartOperationInvocationAssert.FreshLaunchAttemptedWithoutExistingSessionGate(
            existingSessionGateService,
            launchService,
            expectedEditorMode: DaemonEditorMode.Batchmode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenGuiEditorModeRequiresFreshLaunch_StartsFreshDaemonWithGuiMode ()
    {
        var launchService = new RecordingDaemonLaunchService();
        var operation = CreateOperation(
            daemonSessionStore: new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null)),
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: new RecordingDaemonExistingSessionGateService(),
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-gui-launch-mode"),
            TimeSpan.FromMilliseconds(500),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        DaemonStartOperationInvocationAssert.FreshLaunchAttempted(launchService, expectedEditorMode: DaemonEditorMode.Gui);
    }
}
