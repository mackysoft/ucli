using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStartOperationTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartOperationExistingSessionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionGateServiceReturnsAlreadyRunning_ReturnsWithoutLaunch ()
    {
        var existingSession = DaemonSessionTestFactory.Create(processId: 2020);
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(existingSession));
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var existingSessionGateService = new RecordingDaemonExistingSessionGateService
        {
            NextResult = DaemonStartResult.AlreadyRunning(existingSession, IpcUnityEditorObservationTestFactory.Create()),
        };
        var launchService = new RecordingDaemonLaunchService();
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-existing"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.Equal(existingSession, result.Session);
        var gateInvocation = DaemonStartOperationInvocationAssert.ExistingSessionReturnedWithoutFreshLaunch(
            cleanupService,
            existingSessionGateService,
            launchService,
            existingSession);
        Assert.Null(gateInvocation.EditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionGateServiceReturnsAlreadyRunning_DoesNotProbeGuiEditor ()
    {
        var existingSession = DaemonSessionTestFactory.Create(processId: 2022);
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(existingSession));
        var existingSessionGateService = new RecordingDaemonExistingSessionGateService
        {
            NextResult = DaemonStartResult.AlreadyRunning(existingSession, IpcUnityEditorObservationTestFactory.Create()),
        };
        var guiAttachService = new RecordingDaemonGuiEditorAttachService
        {
            NextResult = DaemonStartResult.AlreadyRunning(DaemonSessionTestFactory.Create(processId: 3030), IpcUnityEditorObservationTestFactory.Create()),
        };
        var launchService = new RecordingDaemonLaunchService();
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService,
            daemonGuiEditorAttachService: guiAttachService);

        var result = await operation.StartAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-existing-priority"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.AlreadyRunning, result.Status);
        Assert.Equal(existingSession, result.Session);
        DaemonStartOperationInvocationAssert.ExistingSessionGateAttempted(existingSessionGateService, expectedSession: existingSession);
        DaemonStartOperationInvocationAssert.ExistingSessionTookPrecedenceOverGuiAttachAndFreshLaunch(
            guiAttachService,
            launchService);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionExists_PropagatesRequestedEditorModeToGate ()
    {
        var existingSession = DaemonSessionTestFactory.Create(processId: 2021);
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(existingSession));
        var existingSessionGateService = new RecordingDaemonExistingSessionGateService
        {
            NextResult = DaemonStartResult.AlreadyRunning(existingSession, IpcUnityEditorObservationTestFactory.Create()),
        };
        var launchService = new RecordingDaemonLaunchService();
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-existing-editor-mode"),
            TimeSpan.FromMilliseconds(500),
            editorMode: DaemonEditorMode.Batchmode,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var gateInvocation = DaemonStartOperationInvocationAssert.ExistingSessionGateAttemptedWithoutFreshLaunch(
            existingSessionGateService,
            launchService,
            existingSession);
        Assert.Equal(DaemonEditorMode.Batchmode, gateInvocation.EditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionGateServiceReturnsNull_ContinuesLaunchFlow ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-stale-cleaned");
        var existingSession = DaemonSessionTestFactory.Create(processId: 4242, projectFingerprint: context.ProjectFingerprint);
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(existingSession));
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var existingSessionGateService = new RecordingDaemonExistingSessionGateService
        {
            NextResult = null,
        };
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 8888, projectFingerprint: context.ProjectFingerprint), IpcUnityEditorObservationTestFactory.Create()),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Started, result.Status);
        Assert.True(result.IsSuccess);
        DaemonStartOperationInvocationAssert.ExistingSessionGateAttempted(existingSessionGateService, context, existingSession);
        DaemonStartOperationInvocationAssert.FreshLaunchAttempted(launchService, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionGateServiceReturnsNullAndGuiAttachSucceeds_ReturnsAttachedWithoutFreshLaunch ()
    {
        var context = ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-existing-gui-handoff");
        var existingSession = DaemonSessionTestFactory.Create(
            processId: 4243,
            projectFingerprint: context.ProjectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false);
        var attachedSession = DaemonSessionTestFactory.Create(
            processId: 4243,
            projectFingerprint: context.ProjectFingerprint,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false);
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(existingSession));
        var existingSessionGateService = new RecordingDaemonExistingSessionGateService
        {
            NextResult = null,
        };
        var guiAttachService = new RecordingDaemonGuiEditorAttachService
        {
            NextResult = DaemonStartResult.Attached(attachedSession, IpcUnityEditorObservationTestFactory.Create(editorMode: DaemonEditorMode.Gui)),
        };
        var launchService = new RecordingDaemonLaunchService
        {
            NextResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create(processId: 8889, projectFingerprint: context.ProjectFingerprint), IpcUnityEditorObservationTestFactory.Create()),
        };
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: new RecordingDaemonSessionCleanupService(),
            daemonExistingSessionGateService: existingSessionGateService,
            daemonGuiEditorAttachService: guiAttachService,
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            context,
            TimeSpan.FromMilliseconds(500),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Attached, result.Status);
        Assert.Equal(attachedSession, result.Session);
        DaemonStartOperationInvocationAssert.ExistingSessionGateAttempted(existingSessionGateService, context, existingSession);
        var attachInvocation = DaemonStartOperationInvocationAssert.GuiAttachReturnedWithoutFreshLaunch(
            guiAttachService,
            launchService,
            context);
        Assert.Equal(DaemonEditorMode.Gui, attachInvocation.EditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenExistingSessionGateServiceReturnsFailure_ReturnsFailureWithoutLaunch ()
    {
        var existingSession = DaemonSessionTestFactory.Create(processId: 8080);
        var expectedError = ExecutionError.InternalError("probe failed");
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(existingSession));
        var cleanupService = new RecordingDaemonSessionCleanupService();
        var existingSessionGateService = new RecordingDaemonExistingSessionGateService
        {
            NextResult = DaemonStartResult.Failure(expectedError),
        };
        var launchService = new RecordingDaemonLaunchService();
        var operation = CreateOperation(
            daemonSessionStore: sessionStore,
            daemonSessionCleanupService: cleanupService,
            daemonExistingSessionGateService: existingSessionGateService,
            daemonLaunchService: launchService);

        var result = await operation.StartAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-start-existing-failed"),
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.Equal(expectedError, result.Error);
        DaemonStartOperationInvocationAssert.ExistingSessionGateAttemptedWithoutFreshLaunch(
            existingSessionGateService,
            launchService,
            existingSession);
    }
}
