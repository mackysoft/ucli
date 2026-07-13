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
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-running"),
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(IpcEditorLifecycleState.Ready, result.LifecycleSnapshot!.LifecycleState);
        Assert.True(result.LifecycleSnapshot.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryHandleExistingSession_WhenPingReportsCompiling_ReturnsAlreadyRunningWithLifecycleSnapshot ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 4010);
        var service = DaemonExistingSessionGateServiceTestSupport.CreateService(
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(
                DaemonExistingSessionGateServiceTestSupport.CreatePingResponse(
                    "compiling",
                    "compile",
                    canAcceptExecutionRequests: false)));

        var result = await service.TryHandleExistingSessionAsync(
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-running-compiling"),
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: null,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(IpcEditorLifecycleState.Compiling, result.LifecycleSnapshot!.LifecycleState);
        Assert.Equal(IpcEditorBlockingReason.Compile, result.LifecycleSnapshot.BlockingReason);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
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
            ProjectContextTestFactory.CreateDaemonLifecycleUnityProject("fingerprint-existing-running-mismatch"),
            session,
            TimeSpan.FromMilliseconds(500),
            editorMode: DaemonEditorMode.Gui,
            cancellationToken: CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonEditorModeMismatch, error.Code);
    }
}
