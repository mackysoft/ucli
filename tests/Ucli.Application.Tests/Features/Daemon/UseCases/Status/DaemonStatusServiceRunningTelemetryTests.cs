using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStatusServiceRunningTelemetryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenDaemonIsRunning_MapsPingTelemetryToOutput ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2450);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create() with
        {
            EditorMode = DaemonEditorMode.Gui,
            EditorInstanceId = "editor-instance-1",
        };
        var persistedDiagnosis = DaemonDiagnosisTestFactory.Create();
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(session, persistedDiagnosis));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new IpcUnityEditorObservation(
                serverVersion: "9.9.9",
                unityVersion: "6000.1.4f1",
                projectFingerprint: "project-fingerprint",
                state: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Batchmode,
                    lifecycleState: IpcEditorLifecycleState.DomainReloading,
                    compileState: IpcCompileState.Compiling,
                    generations: new IpcUnityGenerationSnapshot(7, 11, 0, 0),
                    playMode: new IpcPlayModeSnapshot(
                        IpcPlayModeState.Stopped,
                        IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                observedAtUtc: DateTimeOffset.UnixEpoch));
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new StubDaemonReachabilityClassifier(static _ => false),
            new RecordingDaemonSessionDiagnosisResolver(),
            timeProvider);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal("9.9.9", output.ServerVersion);
        Assert.Equal(DaemonEditorMode.Batchmode, output.EditorMode);
        Assert.Equal(IpcEditorLifecycleState.DomainReloading, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReason.DomainReload, output.BlockingReason);
        Assert.Equal(IpcCompileState.Compiling, output.CompileState);
        Assert.Equal(7, output.Generations!.CompileGeneration);
        Assert.Equal(11, output.Generations.DomainReloadGeneration);
        Assert.False(output.CanAcceptExecutionRequests);
        DaemonServiceOutputAssert.SessionMatches(session, output.Session);
        Assert.Null(output.Diagnosis);
        DaemonStatusServiceInvocationAssert.DaemonPingTelemetryRead(
            pingInfoClient,
            context,
            expectedTimeout: context.Timeout,
            expectedSessionToken: session.SessionToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenRunningGuiSessionIsInPlaymode_ReturnsGuiSessionAndReadinessSnapshot ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2455);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create() with
        {
            EditorMode = DaemonEditorMode.Gui,
            OwnerKind = DaemonSessionOwnerKind.User,
            CanShutdownProcess = false,
        };
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(session));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new IpcUnityEditorObservation(
                serverVersion: "9.9.10",
                unityVersion: "6000.1.4f1",
                projectFingerprint: "project-fingerprint",
                state: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Gui,
                    lifecycleState: IpcEditorLifecycleState.PlayMode,
                    compileState: IpcCompileState.Ready,
                    generations: new IpcUnityGenerationSnapshot(8, 12, 0, 1),
                    playMode: new IpcPlayModeSnapshot(
                        IpcPlayModeState.Playing,
                        IpcPlayModeTransition.None,
                        IsPlaying: true,
                        IsPlayingOrWillChangePlaymode: true)),
                observedAtUtc: DateTimeOffset.UnixEpoch));
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new StubDaemonReachabilityClassifier(static _ => false),
            new RecordingDaemonSessionDiagnosisResolver());

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(DaemonEditorMode.Gui, output.EditorMode);
        Assert.Equal(IpcEditorLifecycleState.PlayMode, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReason.PlayMode, output.BlockingReason);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.NotNull(output.Session);
        Assert.Equal(DaemonEditorMode.Gui, output.Session.EditorMode);
        Assert.Equal(DaemonSessionOwnerKind.User, output.Session.OwnerKind);
        Assert.False(output.Session.CanShutdownProcess);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenRunningPingInfoReadTimesOut_ReturnsUnavailableStaleStatus ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2480);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Running(DaemonSessionTestFactory.Create()));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new TimeoutException("ping timeout"));
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new StubDaemonReachabilityClassifier(static _ => false),
            new RecordingDaemonSessionDiagnosisResolver());

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Stale, output.DaemonStatus);
        Assert.Equal(IpcEditorLifecycleState.Unavailable, output.LifecycleState);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Null(result.Error);
        DaemonStatusServiceInvocationAssert.DaemonPingTelemetryRead(
            pingInfoClient,
            context,
            expectedTimeout: null,
            expectedSessionToken: DaemonSessionTestFactory.Create().SessionToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenRunningPingInfoReadFailsUnexpectedly_ReturnsInternalError ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2490);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Running(DaemonSessionTestFactory.Create()));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new InvalidOperationException("broken pipe"));
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new StubDaemonReachabilityClassifier(static _ => false),
            new RecordingDaemonSessionDiagnosisResolver());

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: null, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Failed to read daemon ping information. broken pipe", error.Message);
        DaemonStatusServiceInvocationAssert.DaemonPingTelemetryRead(
            pingInfoClient,
            context,
            expectedTimeout: null,
            expectedSessionToken: DaemonSessionTestFactory.Create().SessionToken);
    }
}
