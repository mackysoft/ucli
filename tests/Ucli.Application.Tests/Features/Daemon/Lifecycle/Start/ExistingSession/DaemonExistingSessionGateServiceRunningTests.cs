namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

public sealed class DaemonExistingSessionGateServiceRunningTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingSucceeds_ReturnsAlreadyRunning ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 4001);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(
                DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse()));

        var result = await service.TryHandleExistingSessionAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-running")),
            session,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        Assert.Equal(session, result.Session);
        var lifecycleObservation = Assert.IsType<IpcUnityEditorObservation>(result.LifecycleObservation);
        Assert.Equal(IpcEditorLifecycleState.Ready, lifecycleObservation.State.LifecycleState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenProbeBudgetExceedsAttemptCap_CapsInitialPing ()
    {
        var session = DaemonSessionTestFactory.Create(
            processId: 4006,
            editorMode: DaemonEditorMode.Gui);
        var pingClient = new RecordingDaemonPingInfoClient(
            DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse());
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: pingClient);

        var result = await service.TryHandleExistingSessionAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-running-capped")),
            session,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), new ManualTimeProvider()),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        var invocation = Assert.Single(pingClient.Invocations);
        Assert.Equal(DaemonTimeouts.ProbeAttemptTimeoutCap, invocation.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingReportsCompiling_ReturnsAlreadyRunningWithLifecycleSnapshot ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 4010);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(
                DaemonExistingSessionGateServiceTestSupport.CreatePingResponse(IpcEditorLifecycleState.Compiling)));

        var result = await service.TryHandleExistingSessionAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-running-compiling")),
            session,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        Assert.Equal(session, result.Session);
        var lifecycleObservation = Assert.IsType<IpcUnityEditorObservation>(result.LifecycleObservation);
        Assert.Equal(IpcEditorLifecycleState.Compiling, lifecycleObservation.State.LifecycleState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenRequestedEditorModeDiffersFromRunningSession_ReturnsMismatch ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 4008);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(
                DaemonExistingSessionGateServiceTestSupport.CreateReadyPingResponse()));

        var result = await service.TryHandleExistingSessionAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject(ProjectFingerprintTestFactory.Create("fingerprint-existing-running-mismatch")),
            session,
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), new ManualTimeProvider()),
            editorMode: DaemonEditorMode.Gui,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonEditorModeMismatch, error.Code);
    }
}
