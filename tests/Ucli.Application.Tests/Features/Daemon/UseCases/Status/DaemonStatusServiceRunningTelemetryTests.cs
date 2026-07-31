using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStatusServiceTestSupport;
using MackySoft.Ucli.Contracts.Editor;

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
            editorMode: UnityEditorMode.Gui,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
        var persistedDiagnosis = DaemonDiagnosisTestFactory.Create();
        var pingResponse = new UnityEditorObservation(
            serverVersion: "9.9.9",
            unityVersion: "6000.1.4f1",
            projectFingerprint: context.Context.UnityProject.ProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: UnityEditorMode.Batchmode,
                lifecycleState: UnityEditorLifecycleState.DomainReloading,
                compileState: UnityEditorCompileState.Compiling,
                generations: new UnityEditorGenerationSnapshot(7, 11, 0, 0),
                playMode: new UnityEditorPlayModeSnapshot(
                    UnityEditorPlayModeState.Stopped,
                    UnityEditorPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: DateTimeOffset.UnixEpoch,
            actionRequired: null,
            primaryDiagnostic: null);
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Running(session, pingResponse, persistedDiagnosis));
        var service = CreateService(resolver, daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal("9.9.9", output.ServerVersion);
        Assert.Equal(UnityEditorMode.Batchmode, output.EditorMode);
        Assert.Equal(UnityEditorLifecycleState.DomainReloading, output.LifecycleState);
        Assert.Equal(UnityEditorBlockingReason.DomainReload, output.BlockingReason);
        Assert.Equal(UnityEditorCompileState.Compiling, output.CompileState);
        Assert.Equal(7, output.Generations!.CompileGeneration);
        Assert.Equal(11, output.Generations.DomainReloadGeneration);
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
            editorMode: UnityEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
        var pingResponse = new UnityEditorObservation(
            serverVersion: "9.9.10",
            unityVersion: "6000.1.4f1",
            projectFingerprint: context.Context.UnityProject.ProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: UnityEditorMode.Gui,
                lifecycleState: UnityEditorLifecycleState.PlayMode,
                compileState: UnityEditorCompileState.Ready,
                generations: new UnityEditorGenerationSnapshot(8, 12, 0, 1),
                playMode: new UnityEditorPlayModeSnapshot(
                    UnityEditorPlayModeState.Playing,
                    UnityEditorPlayModeTransition.None,
                    IsPlaying: true,
                    IsPlayingOrWillChangePlaymode: true)),
            observedAtUtc: DateTimeOffset.UnixEpoch,
            actionRequired: null,
            primaryDiagnostic: null);
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Running(session, pingResponse, diagnosis: null));
        var service = CreateService(resolver, daemonStatusOperation);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(UnityEditorMode.Gui, output.EditorMode);
        Assert.Equal(UnityEditorLifecycleState.PlayMode, output.LifecycleState);
        Assert.Equal(UnityEditorBlockingReason.PlayMode, output.BlockingReason);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.NotNull(output.Session);
        Assert.Equal(UnityEditorMode.Gui, output.Session.EditorMode);
        Assert.Equal(DaemonSessionOwnerKind.User, output.Session.OwnerKind);
        Assert.False(output.Session.CanShutdownProcess);
    }

}
