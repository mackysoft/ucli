using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStatusServiceRunningTelemetryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonIsRunning_MapsObservedPingTelemetryToOutput ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2450);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create(
            editorMode: "gui",
            editorInstanceId: Guid.NewGuid());
        var persistedDiagnosis = DaemonDiagnosisTestFactory.Create();
        var pingResponse = new IpcPingResponse(
            ServerVersion: "9.9.9",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: IpcCompileStateCodec.Compiling,
            LifecycleState: IpcEditorLifecycleStateCodec.DomainReloading,
            BlockingReason: IpcEditorBlockingReasonCodec.DomainReload,
            CompileGeneration: "7",
            DomainReloadGeneration: "11",
            CanAcceptExecutionRequests: false);
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Running(session, pingResponse, persistedDiagnosis));
        var service = CreateService(resolver, daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal("9.9.9", output.ServerVersion);
        Assert.Equal("batchmode", output.EditorMode);
        Assert.Equal(IpcEditorLifecycleStateCodec.DomainReloading, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReasonCodec.DomainReload, output.BlockingReason);
        Assert.Equal(IpcCompileStateCodec.Compiling, output.CompileState);
        Assert.Equal("7", output.CompileGeneration);
        Assert.Equal("11", output.DomainReloadGeneration);
        Assert.False(output.CanAcceptExecutionRequests);
        DaemonServiceOutputAssert.SessionMatches(session, output.Session);
        Assert.Null(output.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenRunningGuiSessionIsInPlaymode_ReturnsGuiSessionAndReadinessSnapshot ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2455);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create(
            editorMode: "gui",
            ownerKind: "user",
            canShutdownProcess: false,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
        var pingResponse = new IpcPingResponse(
            ServerVersion: "9.9.10",
            EditorMode: "gui",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: IpcCompileStateCodec.Ready,
            LifecycleState: IpcEditorLifecycleStateCodec.Playmode,
            BlockingReason: IpcEditorBlockingReasonCodec.PlayMode,
            CompileGeneration: "8",
            DomainReloadGeneration: "12",
            CanAcceptExecutionRequests: true);
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Running(session, pingResponse));
        var service = CreateService(resolver, daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal("gui", output.EditorMode);
        Assert.Equal(IpcEditorLifecycleStateCodec.Playmode, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReasonCodec.PlayMode, output.BlockingReason);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.NotNull(output.Session);
        Assert.Equal("gui", output.Session.EditorMode);
        Assert.Equal("user", output.Session.OwnerKind);
        Assert.False(output.Session.CanShutdownProcess);
    }
}
