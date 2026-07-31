using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayExitServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayExitServiceUnityFailureTests
{
    public static TheoryData<UcliCode> AllowedBlockedErrorCodes => new()
    {
        PlayModeErrorCodes.PlayModeRequiresGuiEditor,
        PlayModeErrorCodes.PlayModeStateUnknown,
        PlayModeErrorCodes.PlayModeAlreadyChanging,
        PlayModeErrorCodes.PlayModeTransitionBlocked,
        PlayModeErrorCodes.PlayModeExitRejected,
    };

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenLifecycleStartRejectsGeneration_PreservesTypedStartErrorWithoutActionPayload ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                new UnityRequestResponse(
                    IpcPayloadCodec.SerializeToElement(new { }),
                    [
                        new OperationExecutionError(
                            LifecycleExecutionErrorCodes.GenerationMismatch,
                            "Lifecycle Execution endpoint generation is stale.",
                            "/generation"),
                    ])));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.ExecuteAsync(
            new PlayExitCommandInput(null, 1500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.GenerationMismatch,
            result.Error!.Code);
        Assert.Equal("/generation", result.Error.InstancePath);
        Assert.Null(result.FailureContext);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTerminalPublicationFails_RetainsTypedResultWithRecoveryReference ()
    {
        var transition = CreateExitedResponse().Result;
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateRecoverableErrorResponse(
                    transition,
                    LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                    "Play Mode exit terminal record could not be published.",
                    ExecutionApplicationState.Applied),
                CreateStartBinding()));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.ExecuteAsync(
            new PlayExitCommandInput(null, 1500),
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
            PlayLifecycleTransitionOutcome.Exited,
            failureContext.Transition!.Result);
        Assert.Equal(
            UnityEditorPlayModeState.Stopped,
            failureContext.CurrentLifecycle!.PlayMode.State);
        Assert.Equal(1500, failureContext.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDeadlineWinsAfterSuccessfulTransition_RetainsResultWithFailedTerminal ()
    {
        var transition = CreateExitedResponse().Result;
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateTerminalErrorResponse(
                    transition,
                    LifecycleExecutionErrorCodes.DeadlineExceeded,
                    "Play Mode exit reached its durable execution deadline.",
                    ExecutionApplicationState.Applied),
                CreateStartBinding()));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.ExecuteAsync(
            new PlayExitCommandInput(null, 1500),
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
            PlayLifecycleTransitionOutcome.Exited,
            failureContext.Transition!.Result);
        Assert.Equal(ExecutionApplicationState.Applied, failureContext.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsTransitionTimeout_ReturnsFailureWithObservedPayload ()
    {
        var before = CreateSnapshot(
            UnityEditorLifecycleState.PlayMode,
            CreatePlayingPlayMode(),
            playModeGeneration: 2);
        var observed = CreateSnapshot(UnityEditorLifecycleState.PlayMode, new UnityEditorPlayModeSnapshot(
            State: UnityEditorPlayModeState.Exiting,
            Transition: UnityEditorPlayModeTransition.Exiting,
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true),
            playModeGeneration: 2);
        var response = new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Exit,
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
                    "Unity Play Mode exit timed out after 1500 milliseconds."),
                CreateStartBinding()));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, 1500), CancellationToken.None);

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
        Assert.Null(failureContext.Transition.After);
    }

    [Theory]
    [MemberData(nameof(AllowedBlockedErrorCodes))]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsAppliedBlockedTransition_RetainsAllowedErrorWithoutAfter (
        UcliCode errorCode)
    {
        var expectedEditorMode =
            errorCode == PlayModeErrorCodes.PlayModeRequiresGuiEditor
                ? UnityEditorMode.Batchmode
                : UnityEditorMode.Gui;
        var before = CreateSnapshot(
            UnityEditorLifecycleState.PlayMode,
            CreatePlayingPlayMode(),
            playModeGeneration: 2,
            editorMode: expectedEditorMode);
        var observed = CreateSnapshot(
            UnityEditorLifecycleState.SafeMode,
            CreateStoppedPlayMode(),
            playModeGeneration: 3,
            editorMode: expectedEditorMode);
        var response = new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Exit,
            PlayLifecycleTransitionOutcome.Blocked,
            before,
            After: null,
            Observed: observed,
            ApplicationState: ExecutionApplicationState.Applied);
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateErrorResponse(
                    response,
                    errorCode,
                    "Unity Play Mode exit completed but readiness was blocked."),
                CreateStartBinding()));
        var service = CreateService(PlayProjectContext, CreateGuiSessionStore(), requestExecutor);

        var result = await service.ExecuteAsync(new PlayExitCommandInput(null, null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(errorCode, result.Error!.Code);
        Assert.Null(result.Output);
        Assert.Equal(
            ExecutionApplicationState.Applied,
            result.FailureContext!.ApplicationState);
        Assert.Null(result.FailureContext.Transition!.After);
        Assert.Equal(
            UnityEditorLifecycleState.SafeMode,
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
            UnityEditorLifecycleState.PlayMode,
            CreatePlayingPlayMode(),
            playModeGeneration: 2);
        var transition = new PlayLifecycleTransitionResult(
            PlayLifecycleTransitionCommand.Exit,
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
            new PlayExitCommandInput(null, 1500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Error!.Code);
        Assert.Equal(
            ExecutionId,
            result.FailureContext!.LifecycleExecutionRef.Id);
        Assert.Null(result.FailureContext.Transition);
    }

}
