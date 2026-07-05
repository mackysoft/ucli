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
            EditorMode = "gui",
            EditorInstanceId = "editor-instance-1",
        };
        var persistedDiagnosis = DaemonDiagnosisTestFactory.Create();
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(session, persistedDiagnosis));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new IpcPingResponse(
                ServerVersion: "9.9.9",
                EditorMode: "batchmode",
                UnityVersion: "6000.1.4f1",
                ProjectFingerprint: "project-fingerprint",
                CompileState: IpcCompileStateCodec.Compiling,
                LifecycleState: IpcEditorLifecycleStateCodec.DomainReloading,
                BlockingReason: IpcEditorBlockingReasonCodec.DomainReload,
                CompileGeneration: "7",
                DomainReloadGeneration: "11",
                CanAcceptExecutionRequests: false));
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
        Assert.Equal("batchmode", output.EditorMode);
        Assert.Equal(IpcEditorLifecycleStateCodec.DomainReloading, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReasonCodec.DomainReload, output.BlockingReason);
        Assert.Equal(IpcCompileStateCodec.Compiling, output.CompileState);
        Assert.Equal("7", output.CompileGeneration);
        Assert.Equal("11", output.DomainReloadGeneration);
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
            EditorMode = "gui",
            OwnerKind = "user",
            CanShutdownProcess = false,
        };
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(session));
        var pingInfoClient = new RecordingDaemonPingInfoClient(new IpcPingResponse(
                ServerVersion: "9.9.10",
                EditorMode: "gui",
                UnityVersion: "6000.1.4f1",
                ProjectFingerprint: "project-fingerprint",
                CompileState: IpcCompileStateCodec.Ready,
                LifecycleState: IpcEditorLifecycleStateCodec.Playmode,
                BlockingReason: IpcEditorBlockingReasonCodec.PlayMode,
                CompileGeneration: "8",
                DomainReloadGeneration: "12",
                CanAcceptExecutionRequests: true));
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
        Assert.Equal("gui", output.EditorMode);
        Assert.Equal(IpcEditorLifecycleStateCodec.Playmode, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReasonCodec.PlayMode, output.BlockingReason);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.NotNull(output.Session);
        Assert.Equal("gui", output.Session.EditorMode);
        Assert.Equal("user", output.Session.OwnerKind);
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
        Assert.Equal(IpcEditorLifecycleStateCodec.Unavailable, output.LifecycleState);
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
