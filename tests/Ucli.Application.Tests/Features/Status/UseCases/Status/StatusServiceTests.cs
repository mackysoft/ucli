using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Observation;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Preflight;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Status;

public sealed class StatusServiceTests
{
    private static readonly ProjectContext StatusProjectContext = ProjectContextTestFactory.CreateSingleRootProject(
        unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonIsRunning_ReturnsObservedPingInfoAndResolvedUnityVersion ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            projectFingerprint: "project-fingerprint",
            endpointAddress: "ucli-daemon-status");
        var pingResponse = new IpcPingResponse(
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
                Generation: "2"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Running(session, pingResponse));
        var service = CreateService(contextResolver, unityVersionResolver, daemonStatusOperation);

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
        DaemonStatusOperationAssert.StatusRequested(
            daemonStatusOperation,
            StatusProjectContext,
            TimeSpan.FromMilliseconds(expectedTimeoutMilliseconds.Value),
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonIsNotRunning_ReturnsStatusWithNullPingFields ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.NotRunning());
        var service = CreateService(contextResolver, unityVersionResolver, daemonStatusOperation);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        StatusServiceAssert.NotRunningOutputReturnedWithoutPingTelemetry(
            result,
            expectedUnityVersion: "6000.1.4f1");
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
        var service = CreateService(contextResolver, unityVersionResolver, daemonStatusOperation);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        StatusServiceAssert.StaleOutputReturnedWithoutPingTelemetry(
            result,
            expectedUnityVersion: "6000.1.4f1");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTimeoutMillisecondsIsInvalid_ReturnsInvalidArgumentWithoutDaemonCall ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.NotRunning());
        var service = CreateService(contextResolver, unityVersionResolver, daemonStatusOperation);

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
        var service = CreateService(contextResolver, unityVersionResolver, daemonStatusOperation);

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
        var service = CreateService(contextResolver, unityVersionResolver, daemonStatusOperation);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        StatusServiceAssert.DaemonStatusFailureReturned(
            result,
            expectedMessage: "Failed to read daemon session.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenRunningPingResponseIsMissing_ReturnsInternalError ()
    {
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(StatusProjectContext));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(new DaemonStatusResult(
            DaemonStatusKind.Running,
            DaemonSessionTestFactory.Create(),
            Diagnosis: null,
            Error: null,
            PingResponse: null));
        var service = CreateService(contextResolver, unityVersionResolver, daemonStatusOperation);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("Daemon status is running but daemon ping response is missing.", error.Message);
    }

    private static StatusService CreateService (
        IProjectContextResolver contextResolver,
        IUnityVersionResolver unityVersionResolver,
        IDaemonStatusOperation daemonStatusOperation)
    {
        return new StatusService(
            new StatusExecutionContextResolver(contextResolver, unityVersionResolver),
            new StatusDaemonObservationService(daemonStatusOperation));
    }
}
