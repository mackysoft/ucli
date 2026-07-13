using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayEnterServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayEnterServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenProjectResolutionFails_ReturnsFailureWithoutSessionOrIpcCall ()
    {
        var expectedError = ExecutionError.InvalidArgument("Project resolution failed.");
        var sessionStore = new UnexpectedDaemonSessionStore();
        var requestExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(ProjectContextResolutionResult.Failure(expectedError), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput("/missing/project", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error!.Code);
        Assert.Equal(expectedError.Message, result.Error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionIsMissing_ReturnsSessionNotAvailableWithoutIpcCall ()
    {
        var context = PlayProjectContext;
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(null));
        var requestExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(context, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(PlayModeErrorCodes.PlayModeSessionNotAvailable, result.Error!.Code);
        DaemonSessionStoreAssert.SessionReadRequestedFor(sessionStore, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenRegisteredSessionIsBatchmode_ReturnsRequiresGuiEditorWithoutIpcCall ()
    {
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(DaemonSessionTestFactory.CreateUserOwned("batchmode", PlaySessionEndpointAddress)));
        var requestExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(PlayModeErrorCodes.PlayModeRequiresGuiEditor, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenEnterSucceeds_ReturnsFlatPayloadAndTransition ()
    {
        var context = PlayProjectContext;
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(DaemonSessionTestFactory.CreateUserOwned("gui", PlaySessionEndpointAddress)));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateEnteredResponse())));
        var service = CreateService(context, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput("/repo/UnityProject", 1500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayEnterExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(context.UnityProject.UnityProjectRoot, output.Project.ProjectPath);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal("gui", output.EditorMode);
        Assert.Equal("playmode", output.LifecycleState);
        Assert.Equal("playMode", output.BlockingReason);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Equal("playing", output.PlayMode.State);
        Assert.Equal("3", output.PlayMode.Generation);
        Assert.Equal(1500, output.TimeoutMilliseconds);
        Assert.Equal(IpcPlayTransitionCommandNames.Enter, output.Transition.Transition);
        Assert.Equal(IpcPlayTransitionResultNames.Entered, output.Transition.Result);
        Assert.NotNull(output.Transition.Before);
        Assert.NotNull(output.Transition.After);
        Assert.Null(output.Transition.Observed);
        Assert.Null(output.Transition.ApplicationState);

        UnityRequestExecutorInvocationAssert.PlayEnterOnce(
            requestExecutor,
            TimeSpan.FromMilliseconds(2500),
            expectedPayloadTimeoutMilliseconds: 1500);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenEnterResponseIsLostDuringDomainReload_ReturnsFailureWithoutServiceRetry ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                UcliCoreErrorCodes.InternalError,
                "Failed to execute Unity daemon IPC request. IPC stream ended before a complete frame was read.")));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, 1500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Error!.Code);
        UnityRequestExecutorInvocationAssert.ExecutedOnce(requestExecutor, UcliCommandIds.PlayEnter);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAlreadyPlaying_ReturnsAlreadyEnteredWithoutGenerationChange ()
    {
        var before = CreateSnapshot("playmode", "playMode", false, CreatePlayMode(
            "playing",
            "none",
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true,
            generation: "9"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Enter,
            IpcPlayTransitionResultNames.AlreadyEntered,
            before)
        {
            After = before,
        });
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(DaemonSessionTestFactory.CreateUserOwned("gui", PlaySessionEndpointAddress)));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayEnterExecutionOutput>(result.Output);
        Assert.Equal(IpcPlayTransitionResultNames.AlreadyEntered, output.Transition.Result);
        Assert.Equal("9", output.Transition.Before.PlayMode!.Generation);
        Assert.Equal("9", output.Transition.After!.PlayMode!.Generation);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsTransitionTimeout_ReturnsFailureWithObservedPayload ()
    {
        var before = CreateSnapshot("ready", null, true, CreateStoppedPlayMode("2"));
        var observed = CreateSnapshot("playmode", "playMode", false, CreatePlayMode(
            "entering",
            "entering",
            isPlaying: false,
            isPlayingOrWillChangePlaymode: true,
            generation: "2"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Enter,
            IpcPlayTransitionResultNames.Timeout,
            before)
        {
            Observed = observed,
            ApplicationState = IpcPlayApplicationStateNames.Indeterminate,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            response,
            PlayModeErrorCodes.PlayModeTransitionTimeout,
            "Unity Play Mode enter timed out after 1500 milliseconds.")));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, 1500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeTransitionTimeout, result.Error!.Code);
        Assert.NotNull(result.Output);
        Assert.Equal(IpcPlayTransitionResultNames.Timeout, result.Output!.Transition.Result);
        Assert.Equal(IpcPlayApplicationStateNames.Indeterminate, result.Output.Transition.ApplicationState);
        Assert.Equal(observed.PlayMode!.State, result.Output.Transition.Observed!.PlayMode!.State);
        Assert.Equal(observed.PlayMode.Transition, result.Output.Transition.Observed.PlayMode.Transition);
        Assert.Equal(observed.PlayMode.Generation, result.Output.Transition.Observed.PlayMode.Generation);
        Assert.Null(result.Output.Transition.After);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsBlockedTransition_ReturnsFailureWithObservedPayload ()
    {
        var before = CreateSnapshot("compiling", "compile", false, CreateStoppedPlayMode("2"));
        var response = new IpcPlayTransitionResponse(new IpcPlayTransitionResult(
            IpcPlayTransitionCommandNames.Enter,
            IpcPlayTransitionResultNames.Blocked,
            before)
        {
            Observed = before,
            ApplicationState = IpcPlayApplicationStateNames.NotApplied,
        });
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponse(
            response,
            PlayModeErrorCodes.PlayModeTransitionBlocked,
            "Unity Play Mode enter is blocked.")));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeTransitionBlocked, result.Error!.Code);
        Assert.NotNull(result.Output);
        Assert.Equal(IpcPlayTransitionResultNames.Blocked, result.Output!.Transition.Result);
        Assert.Equal(IpcPlayApplicationStateNames.NotApplied, result.Output.Transition.ApplicationState);
        Assert.Equal("compiling", result.Output.LifecycleState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnitySuccessPayloadIsInvalid_ReturnsInternalError ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateInvalidPayloadResponse()));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Error!.Code);
        Assert.Contains("Unity play enter payload is invalid.", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityErrorOmitsTransitionPayload_ReturnsOriginalError ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateErrorResponseWithoutTransitionPayload(
            UcliCoreErrorCodes.InvalidArgument,
            "Unity play enter payload is invalid.")));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error!.Code);
        Assert.Equal("Unity play enter payload is invalid.", result.Error.Message);
    }

}
