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
            projectFingerprint: StatusProjectContext.UnityProject.ProjectFingerprint,
            endpointAddress: "ucli-daemon-status");
        var pingResponse = CreatePingResponse(
            lifecycleState: IpcEditorLifecycleState.Busy,
            generations: new IpcUnityGenerationSnapshot(
                CompileGeneration: 12,
                DomainReloadGeneration: 7,
                AssetRefreshGeneration: 3,
                PlayModeGeneration: 2));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Running(session, pingResponse, diagnosis: null));
        var service = CreateService(contextResolver, unityVersionResolver, daemonStatusOperation);

        var result = await service.ExecuteAsync(new StatusCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<StatusExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal("6000.1.4f1", output.UnityVersion);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal(IpcEditorLifecycleState.Busy, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReason.Busy, output.BlockingReason);
        Assert.Equal(IpcCompileState.Ready, output.CompileState);
        Assert.NotNull(output.Generations);
        Assert.Equal(12, output.Generations.CompileGeneration);
        Assert.Equal(7, output.Generations.DomainReloadGeneration);
        Assert.Equal(3, output.Generations.AssetRefreshGeneration);
        Assert.Equal(2, output.Generations.PlayModeGeneration);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Equal(DaemonEditorMode.Batchmode, output.EditorMode);
        Assert.NotNull(output.PlayMode);
        Assert.Equal(IpcPlayModeState.Stopped, output.PlayMode.State);
        Assert.Equal(IpcPlayModeTransition.None, output.PlayMode.Transition);
        Assert.False(output.PlayMode.IsPlaying);
        Assert.False(output.PlayMode.IsPlayingOrWillChangePlaymode);
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
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.NotRunning(
            diagnosis: null,
            lastLaunchAttempt: null));
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
            projectFingerprint: StatusProjectContext.UnityProject.ProjectFingerprint,
            endpointAddress: "ucli-daemon-status"), diagnosis: null));
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
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.NotRunning(
            diagnosis: null,
            lastLaunchAttempt: null));
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
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.NotRunning(
            diagnosis: null,
            lastLaunchAttempt: null));
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

    private static StatusService CreateService (
        IProjectContextResolver contextResolver,
        IUnityVersionResolver unityVersionResolver,
        IDaemonStatusOperation daemonStatusOperation)
    {
        return new StatusService(
            new StatusExecutionContextResolver(contextResolver, unityVersionResolver),
            new StatusDaemonObservationService(daemonStatusOperation));
    }

    private static IpcUnityEditorObservation CreatePingResponse (
        IpcEditorLifecycleState lifecycleState = IpcEditorLifecycleState.Ready,
        IpcUnityGenerationSnapshot? generations = null)
    {
        return new IpcUnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "2022.3.5f1",
            projectFingerprint: StatusProjectContext.UnityProject.ProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Batchmode,
                lifecycleState: lifecycleState,
                compileState: IpcCompileState.Ready,
                generations: generations ?? new IpcUnityGenerationSnapshot(0, 0, 0, 0),
                playMode: new IpcPlayModeSnapshot(
                    State: IpcPlayModeState.Stopped,
                    Transition: IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            actionRequired: null,
            primaryDiagnostic: null);
    }
}
