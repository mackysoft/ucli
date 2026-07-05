using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Observation;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Preflight;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Status;

public sealed class StatusServiceTests
{
    private static readonly ProjectContext StatusProjectContext = ProjectContextTestFactory.CreateSingleRootProject(
        unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonIsRunning_ReturnsPingInfoAndResolvedUnityVersion ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            projectFingerprint: "project-fingerprint",
            endpointAddress: "ucli-daemon-status")));
        var daemonPingInfoClient = new RecordingDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "2022.3.5f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: "ready",
            LifecycleState: "busy",
            BlockingReason: "busy",
            CompileGeneration: "12",
            DomainReloadGeneration: "7",
            CanAcceptExecutionRequests: false,
            PlayMode: new IpcPlayModeSnapshot(
                State: "stopped",
                Transition: "none",
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: "2")));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<StatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal("6000.1.4f1", output.UnityVersion);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal("busy", output.LifecycleState);
        Assert.Equal("busy", output.BlockingReason);
        Assert.Equal("ready", output.CompileState);
        Assert.Equal("12", output.CompileGeneration);
        Assert.Equal("7", output.DomainReloadGeneration);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Equal("batchmode", output.EditorMode);
        Assert.NotNull(output.PlayMode);
        Assert.Equal("stopped", output.PlayMode.State);
        Assert.Equal("none", output.PlayMode.Transition);
        Assert.False(output.PlayMode.IsPlaying);
        Assert.False(output.PlayMode.IsPlayingOrWillChangePlaymode);
        Assert.Equal("2", output.PlayMode.Generation);
        var expectedTimeoutMilliseconds = UcliConfig.CreateDefault().IpcTimeoutMillisecondsByCommand[UcliCommandIds.Status.Name];
        Assert.NotNull(expectedTimeoutMilliseconds);
        Assert.Equal(expectedTimeoutMilliseconds, output.TimeoutMilliseconds);
        var expectedTimeout = TimeSpan.FromMilliseconds(expectedTimeoutMilliseconds.Value);
        DaemonStatusOperationAssert.StatusRequested(
            daemonStatusOperation,
            StatusProjectContext,
            expectedTimeout,
            CancellationToken.None);
        DaemonPingInfoClientAssert.PingReadForSession(
            daemonPingInfoClient,
            StatusProjectContext,
            expectedTimeout,
            "session-token",
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonIsNotRunning_ReturnsStatusWithNullPingFields ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.NotRunning());
        var daemonPingInfoClient = new RecordingDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "2022.3.5f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        StatusServiceAssert.NotRunningOutputReturnedWithoutPingTelemetry(
            result,
            expectedUnityVersion: "6000.1.4f1",
            daemonPingInfoClient);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonIsStale_ReturnsStatusWithNullPingFields ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Stale(DaemonSessionTestFactory.Create(
            sessionToken: "stale-session-token",
            projectFingerprint: "project-fingerprint",
            endpointAddress: "ucli-daemon-status")));
        var daemonPingInfoClient = new RecordingDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "2022.3.5f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        StatusServiceAssert.StaleOutputReturnedWithoutPingTelemetry(
            result,
            expectedUnityVersion: "6000.1.4f1",
            daemonPingInfoClient);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTimeoutMillisecondsIsInvalid_ReturnsInvalidArgumentWithoutDaemonCall ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.NotRunning());
        var daemonPingInfoClient = new RecordingDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "2022.3.5f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, 0), CancellationToken.None);

        StatusServiceAssert.InvalidTimeoutStoppedBeforeDaemonStatus(result, daemonStatusOperation);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenContextResolutionFails_ReturnsResolutionError ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Failure(
            ExecutionError.InvalidArgument("Unity project path is invalid.")));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.NotRunning());
        var daemonPingInfoClient = new RecordingDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "2022.3.5f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        StatusServiceAssert.ContextResolutionFailureStoppedBeforeStatusResolution(
            result,
            expectedMessage: "Unity project path is invalid.",
            daemonStatusOperation,
            unityVersionResolver);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonStatusFails_ReturnsDaemonStatusError ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Failure(
            ExecutionError.InternalError("Failed to read daemon session.")));
        var daemonPingInfoClient = new RecordingDaemonPingInfoClient(new IpcPingResponse(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "2022.3.5f1",
            ProjectFingerprint: "project-fingerprint",
            CompileState: "ready"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        StatusServiceAssert.DaemonStatusFailureStoppedBeforePingTelemetry(
            result,
            expectedMessage: "Failed to read daemon session.",
            daemonPingInfoClient);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPingInfoTimesOut_ReturnsUnavailableStaleStatus ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            projectFingerprint: "project-fingerprint",
            endpointAddress: "ucli-daemon-status")));
        var daemonPingInfoClient = new RecordingDaemonPingInfoClient(new TimeoutException("ping timeout"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<StatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Stale, output.DaemonStatus);
        Assert.Equal(IpcEditorLifecycleStateCodec.Unavailable, output.LifecycleState);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPingInfoBecomesUnreachable_ReturnsStaleStatus ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            projectFingerprint: "project-fingerprint",
            endpointAddress: "ucli-daemon-status")));
        var daemonPingInfoClient = new RecordingDaemonPingInfoClient(new SocketException((int)SocketError.ConnectionRefused));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<StatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Stale, output.DaemonStatus);
        Assert.Null(output.ServerVersion);
        Assert.Null(output.CompileState);
        Assert.Null(output.EditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPingInfoFails_ReturnsInternalError ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.Running(DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            projectFingerprint: "project-fingerprint",
            endpointAddress: "ucli-daemon-status")));
        var daemonPingInfoClient = new RecordingDaemonPingInfoClient(new InvalidOperationException("failed"));
        var service = CreateService(
            contextResolver,
            unityVersionResolver,
            daemonStatusOperation,
            daemonPingInfoClient);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("Failed to read daemon ping information", error.Message, StringComparison.Ordinal);
    }

    private static StatusService CreateService (
        IProjectContextResolver contextResolver,
        IUnityVersionResolver unityVersionResolver,
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonPingInfoClient daemonPingInfoClient)
    {
        return new StatusService(
            new StatusExecutionContextResolver(contextResolver, unityVersionResolver),
            new StatusDaemonObservationService(
                daemonStatusOperation,
                daemonPingInfoClient,
                new StubDaemonReachabilityClassifier(static exception => exception is SocketException),
                new RecordingDaemonLifecycleStore(),
                new RecordingDaemonProcessIdentityAssessor()));
    }
}
