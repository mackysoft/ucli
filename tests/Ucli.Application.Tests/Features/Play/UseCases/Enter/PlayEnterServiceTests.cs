using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayEnterServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayEnterServiceTests
{
    private static readonly Guid OtherExecutionId =
        Guid.Parse("2bb6851e-f54f-474b-833d-f466b6fe2219");

    public static TheoryData<UcliCode> AllowedBlockedErrorCodes => new()
    {
        PlayModeErrorCodes.PlayModeRequiresGuiEditor,
        PlayModeErrorCodes.PlayModeStateUnknown,
        PlayModeErrorCodes.PlayModeAlreadyChanging,
        PlayModeErrorCodes.PlayModeTransitionBlocked,
        PlayModeErrorCodes.PlayModeEnterRejected,
    };

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenLifecycleStartRejectsHost_PreservesTypedStartErrorWithoutActionPayload ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                new UnityRequestResponse(
                    IpcPayloadCodec.SerializeToElement(new { }),
                    [
                        new OperationExecutionError(
                            LifecycleExecutionErrorCodes.HostMismatch,
                            "Lifecycle Execution belongs to another Unity host.",
                            "/host"),
                    ])));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.ExecuteAsync(
            new PlayEnterCommandInput(null, 1500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.HostMismatch,
            result.Error!.Code);
        Assert.Equal("/host", result.Error.InstancePath);
        Assert.Null(result.FailureContext);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenProjectResolutionFails_ReturnsFailureWithoutSessionOrIpcCall ()
    {
        var expectedError = ExecutionError.InvalidArgument("Project resolution failed.");
        var sessionStore = new UnexpectedDaemonSessionStore();
        var requestExecutor = new UnexpectedUnityRequestExecutor();
        var service = CreateService(ProjectContextResolutionResult.Failure(expectedError), sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(AbsolutePath.Parse(ProjectPathTestValues.TemporaryUnityProject), null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error!.Code);
        Assert.Equal(expectedError.Message, result.Error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSessionIsMissing_ReturnsSessionNotAvailableWithoutIpcCall ()
    {
        var context = PlayProjectContext;
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing());
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
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(
            DaemonSessionTestFactory.Create(
                editorMode: UnityEditorMode.Batchmode,
                endpointAddress: PlaySessionEndpointAddress)));
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
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(
            DaemonSessionTestFactory.CreateUserOwned(
                UnityEditorMode.Gui,
                PlaySessionEndpointAddress,
                DaemonSessionTestFactory.DefaultEditorInstanceId)));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(CreateEnteredResponse())));
        var service = CreateService(context, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(AbsolutePath.Parse(ProjectPathTestValues.RepositoryUnityProject), 1500), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var output = Assert.IsType<PlayEnterExecutionOutput>(result.Output);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(context.UnityProject.UnityProjectRoot.Value, output.Project.ProjectPath);
        Assert.Equal("0.5.0", output.ServerVersion);
        Assert.Equal(UnityEditorMode.Gui, output.EditorMode);
        Assert.Equal(UnityEditorLifecycleState.PlayMode, output.LifecycleState);
        Assert.Equal(UnityEditorBlockingReason.PlayMode, output.BlockingReason);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Equal(UnityEditorPlayModeState.Playing, output.PlayMode.State);
        Assert.Equal(3, output.Generations!.PlayModeGeneration);
        Assert.Equal(1500, output.TimeoutMilliseconds);
        Assert.Equal(PlayLifecycleTransitionCommand.Enter, output.Transition.Transition);
        Assert.Equal(PlayLifecycleTransitionOutcome.Entered, output.Transition.Result);
        Assert.NotNull(output.Transition.Before);
        Assert.NotNull(output.Transition.After);

        UnityRequestExecutorInvocationAssert.PlayEnterOnce(
            requestExecutor,
            TimeSpan.FromMilliseconds(4500));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenEnterResponseIsLostDuringDomainReload_ReturnsFailureWithoutServiceRetry ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                UnityRequestFailureKind.TransportInterrupted,
                EditorLifecycleErrorCodes.EditorUnavailable,
                "Unity daemon IPC response was interrupted and no successor endpoint became available."),
                CreateStartBinding(),
                lifecycleActionDispatched: true));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, 1500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(ExecutionId, result.FailureContext!.LifecycleExecutionRef.Id);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            result.FailureContext.ApplicationState);
        Assert.Equal(EditorLifecycleErrorCodes.EditorUnavailable, result.Error!.Code);
        UnityRequestExecutorInvocationAssert.ExecutedOnce(requestExecutor, UcliCommandIds.PlayEnter);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTerminalPublicationFails_RetainsTypedResultWithRecoveryReference ()
    {
        var transition = CreateEnteredResponse().Result;
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateRecoverableErrorResponse(
                    transition,
                    LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                    "Play Mode enter terminal record could not be published.",
                    ExecutionApplicationState.Applied),
                CreateStartBinding()));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.ExecuteAsync(
            new PlayEnterCommandInput(null, 1500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            result.Error!.Code);
        var failureContext = result.FailureContext!;
        Assert.NotNull(failureContext);
        Assert.Equal(
            ExecutionLifecycle.Recovery,
            failureContext.LifecycleExecutionRef.Lifecycle);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Publishing),
            failureContext.LifecycleExecutionRef.State.Value);
        Assert.Equal(
            PlayLifecycleTransitionOutcome.Entered,
            failureContext.Transition!.Result);
        Assert.Equal(
            UnityEditorPlayModeState.Playing,
            failureContext.CurrentLifecycle!.PlayMode.State);
        Assert.Equal(1500, failureContext.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDeadlineWinsAfterSuccessfulTransition_RetainsResultWithFailedTerminal ()
    {
        var transition = CreateEnteredResponse().Result;
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateTerminalErrorResponse(
                    transition,
                    LifecycleExecutionErrorCodes.DeadlineExceeded,
                    "Play Mode enter reached its durable execution deadline.",
                    ExecutionApplicationState.Applied),
                CreateStartBinding()));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.ExecuteAsync(
            new PlayEnterCommandInput(null, 1500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(LifecycleExecutionErrorCodes.DeadlineExceeded, result.Error!.Code);
        var failureContext = result.FailureContext!;
        Assert.Equal(
            ExecutionLifecycle.Terminal,
            failureContext.LifecycleExecutionRef.Lifecycle);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Failed),
            failureContext.LifecycleExecutionRef.State.Value);
        Assert.Equal(
            PlayLifecycleTransitionOutcome.Entered,
            failureContext.Transition!.Result);
        Assert.Equal(ExecutionApplicationState.Applied, failureContext.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenResponseIdentifiesAnotherExecution_RetainsTrustedStart ()
    {
        var response = CreateEnteredResponse();
        var crossedResponse = new IpcPlayTransitionResponse(
            CreateTerminalReference(OtherExecutionId),
            response.Result);
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(crossedResponse),
                CreateStartBinding()));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.ExecuteAsync(
            new PlayEnterCommandInput(null, 1500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Error!.Code);
        Assert.Equal(
            ExecutionId,
            result.FailureContext!.LifecycleExecutionRef.Id);
        Assert.Null(result.FailureContext.Transition);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAlreadyPlaying_ReturnsAlreadyEnteredWithoutGenerationChange ()
    {
        var before = CreateSnapshot(UnityEditorLifecycleState.PlayMode, CreatePlayMode(
            UnityEditorPlayModeState.Playing,
            UnityEditorPlayModeTransition.None,
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true),
            playModeGeneration: 9);
        var response = new IpcPlayTransitionResponse(CreateTerminalReference(), new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Enter,
            PlayLifecycleTransitionOutcome.AlreadyEntered,
            before,
            After: before,
            Observed: null,
            ApplicationState: null));
        var sessionStore = new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(
            DaemonSessionTestFactory.CreateUserOwned(
                UnityEditorMode.Gui,
                PlaySessionEndpointAddress,
                DaemonSessionTestFactory.DefaultEditorInstanceId)));
        var requestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(CreateResponse(response)));
        var service = CreateService(PlayProjectContext, sessionStore, requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<PlayEnterExecutionOutput>(result.Output);
        Assert.Equal(PlayLifecycleTransitionOutcome.AlreadyEntered, output.Transition.Result);
        Assert.Equal(9, output.Transition.Before.Generations!.PlayModeGeneration);
        Assert.Equal(9, output.Transition.After!.Generations!.PlayModeGeneration);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsTransitionTimeout_ReturnsFailureWithObservedPayload ()
    {
        var before = CreateSnapshot(
            UnityEditorLifecycleState.Ready,
            CreateStoppedPlayMode(),
            playModeGeneration: 2);
        var observed = CreateSnapshot(UnityEditorLifecycleState.PlayMode, CreatePlayMode(
            UnityEditorPlayModeState.Entering,
            UnityEditorPlayModeTransition.Entering,
            isPlaying: false,
            isPlayingOrWillChangePlaymode: true),
            playModeGeneration: 2);
        var response = new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Enter,
            PlayLifecycleTransitionOutcome.Timeout,
            before,
            After: null,
            Observed: observed,
            ApplicationState: ExecutionApplicationState.Indeterminate);
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateErrorResponse(
                    response,
                    PlayModeErrorCodes.PlayModeTransitionTimeout,
                    "Unity Play Mode enter timed out after 1500 milliseconds."),
                CreateStartBinding()));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, 1500), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PlayModeErrorCodes.PlayModeTransitionTimeout, result.Error!.Code);
        Assert.Null(result.Output);
        var failureContext = result.FailureContext!;
        Assert.NotNull(failureContext);
        Assert.Equal(PlayLifecycleTransitionOutcome.Timeout, failureContext.Transition!.Result);
        Assert.Equal(ExecutionApplicationState.Indeterminate, failureContext.ApplicationState);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Failed),
            failureContext.LifecycleExecutionRef.State.Value);
        Assert.Equal(ExecutionId, failureContext.LifecycleExecutionRef.Id);
        Assert.Equal(observed.State.PlayMode.State, failureContext.Transition.Observed!.PlayMode.State);
        Assert.Equal(observed.State.PlayMode.Transition, failureContext.Transition.Observed.PlayMode.Transition);
        Assert.Equal(
            observed.State.Generations.PlayModeGeneration,
            failureContext.Transition.Observed.Generations!.PlayModeGeneration);
        Assert.Null(failureContext.Transition.After);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTypedTimeoutHasAnotherErrorCode_RetainsOnlyTrustedStart ()
    {
        var before = CreateSnapshot(
            UnityEditorLifecycleState.Ready,
            CreateStoppedPlayMode(),
            playModeGeneration: 2);
        var resultPayload = new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Enter,
            PlayLifecycleTransitionOutcome.Timeout,
            before,
            After: null,
            Observed: before,
            ApplicationState: ExecutionApplicationState.Indeterminate);
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateErrorResponse(
                    resultPayload,
                    UcliCoreErrorCodes.InvalidArgument,
                    "Unexpected error code."),
                CreateStartBinding()));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.ExecuteAsync(
            new PlayEnterCommandInput(null, 1500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Error!.Code);
        Assert.Equal(
            ExecutionId,
            result.FailureContext!.LifecycleExecutionRef.Id);
        Assert.Null(result.FailureContext.Transition);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            result.FailureContext.ApplicationState);
    }

    [Theory]
    [MemberData(nameof(AllowedBlockedErrorCodes))]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsBlockedTransition_RetainsAllowedErrorAndObservedPayload (
        UcliCode errorCode)
    {
        var expectedEditorMode =
            errorCode == PlayModeErrorCodes.PlayModeRequiresGuiEditor
                ? UnityEditorMode.Batchmode
                : UnityEditorMode.Gui;
        var before = CreateSnapshot(
            UnityEditorLifecycleState.Compiling,
            CreateStoppedPlayMode(),
            playModeGeneration: 2,
            editorMode: expectedEditorMode);
        var response = new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Enter,
            PlayLifecycleTransitionOutcome.Blocked,
            before,
            After: null,
            Observed: before,
            ApplicationState: ExecutionApplicationState.NotApplied);
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateErrorResponse(
                    response,
                    errorCode,
                    "Unity Play Mode enter is blocked."),
                CreateStartBinding()));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorCode, result.Error!.Code);
        Assert.Null(result.Output);
        Assert.Equal(
            PlayLifecycleTransitionOutcome.Blocked,
            result.FailureContext!.Transition!.Result);
        Assert.Equal(
            ExecutionApplicationState.NotApplied,
            result.FailureContext.ApplicationState);
        Assert.Equal(
            UnityEditorLifecycleState.Compiling,
            result.FailureContext.CurrentLifecycle!.LifecycleState);
        Assert.Equal(
            expectedEditorMode,
            result.FailureContext.CurrentLifecycle.EditorMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTypedBlockedResultHasUnrelatedErrorCode_RetainsOnlyTrustedStart ()
    {
        var before = CreateSnapshot(
            UnityEditorLifecycleState.Compiling,
            CreateStoppedPlayMode(),
            playModeGeneration: 2);
        var transition = new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Enter,
            PlayLifecycleTransitionOutcome.Blocked,
            before,
            After: null,
            Observed: before,
            ApplicationState: ExecutionApplicationState.NotApplied);
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateErrorResponse(
                    transition,
                    UcliCoreErrorCodes.InvalidArgument,
                    "Unexpected error code."),
                CreateStartBinding()));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.ExecuteAsync(
            new PlayEnterCommandInput(null, 1500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Error!.Code);
        Assert.Equal(
            ExecutionId,
            result.FailureContext!.LifecycleExecutionRef.Id);
        Assert.Null(result.FailureContext.Transition);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnitySuccessPayloadIsInvalid_ReturnsInternalError ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateInvalidPayloadResponse(),
                CreateStartBinding()));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Error!.Code);
        Assert.Contains("Unity play enter payload is invalid.", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(
            ExecutionId,
            result.FailureContext!.LifecycleExecutionRef.Id);
        Assert.Null(result.FailureContext.Transition);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityErrorOmitsTransitionPayload_ReturnsOriginalError ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateErrorResponseWithoutTransitionPayload(
                    UcliCoreErrorCodes.InvalidArgument,
                    "Unity play enter payload is invalid."),
                CreateStartBinding()));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayEnterCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error!.Code);
        Assert.Equal("Unity play enter payload is invalid.", result.Error.Message);
        Assert.Equal(
            ExecutionId,
            result.FailureContext!.LifecycleExecutionRef.Id);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            result.FailureContext.ApplicationState);
        Assert.Null(result.FailureContext.Transition);
    }

}
