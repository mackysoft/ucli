using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Features.Play.Common.Projection;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Executes Play Mode enter against an existing registered GUI daemon session. </summary>
internal sealed class PlayEnterService : IPlayEnterService
{
    private const int ResponseGraceMilliseconds = 1000;
    private const string SessionNotAvailableMessage = "Registered GUI daemon session is not available for Play Mode enter.";
    private const string RequiresGuiEditorMessage = "Play Mode enter requires a registered GUI daemon session.";

    private readonly IPlayCommandExecutionContextResolver contextResolver;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="PlayEnterService" /> class. </summary>
    /// <param name="contextResolver"> The Play Mode command context resolver dependency. </param>
    /// <param name="unityRequestExecutor"> The Unity IPC request executor dependency. </param>
    public PlayEnterService (
        IPlayCommandExecutionContextResolver contextResolver,
        IUnityRequestExecutor unityRequestExecutor)
    {
        this.contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<PlayEnterExecutionResult> ExecuteAsync (
        PlayEnterCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await contextResolver.ResolveAsync(
                input.ProjectPath,
                input.TimeoutMilliseconds,
                UcliCommandIds.PlayEnter,
                SessionNotAvailableMessage,
                RequiresGuiEditorMessage,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return PlayEnterExecutionResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var requestTimeout = context.Timeout + TimeSpan.FromMilliseconds(ResponseGraceMilliseconds);
        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.PlayEnter,
                UnityExecutionMode.Daemon,
                requestTimeout,
                context.ProjectContext.Config,
                context.ProjectContext.UnityProject,
                new UnityRequestPayload.PlayEnter(),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            return PlayEnterExecutionResult.Failure(CreateErrorFromUnityRequestFailure(executionResult.FailureInfo!));
        }

        return CreateResultFromResponse(context, executionResult.Response!);
    }

    private PlayEnterExecutionResult CreateResultFromResponse (
        PlayCommandExecutionContext context,
        UnityRequestResponse response)
    {
        if (response.Errors.Count != 0)
        {
            if (!TryReadTransitionResponse(response, out var errorTransitionResponse, out _))
            {
                return PlayEnterExecutionResult.Failure(CreateErrorFromResponse(response));
            }

            var errorOutputResult = CreateOutput(context, errorTransitionResponse!.Transition);
            if (!errorOutputResult.IsSuccess)
            {
                return PlayEnterExecutionResult.Failure(errorOutputResult.Error!);
            }

            var errorOutput = errorOutputResult.Output!;
            return PlayEnterExecutionResult.Failure(CreateErrorFromResponse(response, errorOutput.Transition), errorOutput);
        }

        if (!TryReadTransitionResponse(response, out var transitionResponse, out var payloadFailure))
        {
            return PlayEnterExecutionResult.Failure(payloadFailure!);
        }

        var outputResult = CreateOutput(context, transitionResponse!.Transition);
        if (!outputResult.IsSuccess)
        {
            return PlayEnterExecutionResult.Failure(outputResult.Error!);
        }

        var output = outputResult.Output!;
        return output.Transition.Result switch
        {
            IpcPlayTransitionOutcome.Entered or IpcPlayTransitionOutcome.AlreadyEntered
                => PlayEnterExecutionResult.Success(output),
            IpcPlayTransitionOutcome.Timeout
                => PlayEnterExecutionResult.Failure(CreateTransitionFailure(
                    PlayModeErrorCodes.PlayModeTransitionTimeout,
                    $"Unity Play Mode enter timed out after {context.TimeoutMilliseconds} milliseconds."),
                    output),
            IpcPlayTransitionOutcome.Blocked
                => PlayEnterExecutionResult.Failure(CreateTransitionFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    "Unity Play Mode enter was blocked by the current editor lifecycle state."),
                    output),
            _ => throw new ArgumentOutOfRangeException(nameof(PlayTransitionOutput.Result), output.Transition.Result, null),
        };
    }

    private static bool TryReadTransitionResponse (
        UnityRequestResponse response,
        out IpcPlayTransitionResponse? transitionResponse,
        out ApplicationFailure? failure)
    {
        if (IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPlayTransitionResponse payload, out var payloadError))
        {
            transitionResponse = payload;
            failure = null;
            return true;
        }

        transitionResponse = null;
        failure = ApplicationFailure.InternalError($"Unity play enter payload is invalid. {payloadError.Message}");
        return false;
    }

    private static PlayEnterOutputCreationResult CreateOutput (
        PlayCommandExecutionContext context,
        IpcPlayTransitionResult transition)
    {
        if (transition.Transition != IpcPlayTransitionCommand.Enter)
        {
            return PlayEnterOutputCreationResult.Failure(ApplicationFailure.InternalError(
                $"Unity play enter transition mismatch. Actual={transition.Transition}."));
        }

        var currentSnapshot = transition.After ?? transition.Observed!;
        var validationFailure = ValidateTransitionSnapshots(context, transition, currentSnapshot);
        if (validationFailure is not null)
        {
            return PlayEnterOutputCreationResult.Failure(validationFailure);
        }

        var lifecycle = PlayOutputProjectionFactory.CreateSnapshotOutput(currentSnapshot);
        if (lifecycle.EditorMode != DaemonEditorMode.Gui)
        {
            return PlayEnterOutputCreationResult.Failure(ApplicationFailure.InternalError(
                RequiresGuiEditorMessage,
                PlayModeErrorCodes.PlayModeRequiresGuiEditor));
        }

        return PlayEnterOutputCreationResult.Success(new PlayEnterExecutionOutput(
            Project: context.Project,
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: lifecycle.ServerVersion,
            EditorMode: DaemonEditorMode.Gui,
            LifecycleState: lifecycle.LifecycleState,
            BlockingReason: lifecycle.BlockingReason,
            CompileState: lifecycle.CompileState,
            Generations: lifecycle.Generations,
            CanAcceptExecutionRequests: lifecycle.CanAcceptExecutionRequests,
            ObservedAtUtc: lifecycle.ObservedAtUtc,
            ActionRequired: lifecycle.ActionRequired,
            PrimaryDiagnostic: lifecycle.PrimaryDiagnostic,
            PlayMode: lifecycle.PlayMode,
            Transition: PlayOutputProjectionFactory.CreateTransitionOutput(transition),
            TimeoutMilliseconds: context.TimeoutMilliseconds));
    }

    private static ApplicationFailure? ValidateTransitionSnapshots (
        PlayCommandExecutionContext context,
        IpcPlayTransitionResult transition,
        IpcUnityEditorObservation currentSnapshot)
    {
        var beforeFailure = ValidateSnapshotProject(context, transition.Before, "before");
        if (beforeFailure is not null)
        {
            return beforeFailure;
        }

        var currentFailure = ValidateSnapshotProject(context, currentSnapshot, "current");
        if (currentFailure is not null)
        {
            return currentFailure;
        }

        return transition.Result switch
        {
            IpcPlayTransitionOutcome.Entered => ValidateEntered(transition.Before, currentSnapshot),
            IpcPlayTransitionOutcome.AlreadyEntered => ValidateAlreadyEntered(transition.Before, currentSnapshot),
            IpcPlayTransitionOutcome.Timeout or IpcPlayTransitionOutcome.Blocked => null,
            _ => throw new ArgumentOutOfRangeException(nameof(IpcPlayTransitionResult.Result), transition.Result, null),
        };
    }

    private static ApplicationFailure? ValidateSnapshotProject (
        PlayCommandExecutionContext context,
        IpcUnityEditorObservation snapshot,
        string label)
    {
        if (snapshot.ProjectFingerprint != context.Project.ProjectFingerprint)
        {
            return ApplicationFailure.InternalError(
                $"Unity play enter {label} projectFingerprint mismatch. Requested={context.Project.ProjectFingerprint}, Actual={snapshot.ProjectFingerprint}.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateEntered (
        IpcUnityEditorObservation before,
        IpcUnityEditorObservation after)
    {
        if (!IsReadyStoppedSnapshot(before))
        {
            return CreateStateUnknownFailure("Unity play enter reported entered without a ready stopped before snapshot.");
        }

        if (!IsEnteredSnapshot(after))
        {
            return CreateStateUnknownFailure("Unity play enter reported entered without a playing snapshot.");
        }

        if (before.State.Generations.PlayModeGeneration == after.State.Generations.PlayModeGeneration)
        {
            return CreateStateUnknownFailure("Unity play enter reported entered without changing generations.playModeGeneration.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateAlreadyEntered (
        IpcUnityEditorObservation before,
        IpcUnityEditorObservation after)
    {
        if (!IsEnteredSnapshot(before) || !IsEnteredSnapshot(after))
        {
            return CreateStateUnknownFailure("Unity play enter reported alreadyEntered without a playing snapshot.");
        }

        if (before.State.Generations.PlayModeGeneration != after.State.Generations.PlayModeGeneration)
        {
            return CreateStateUnknownFailure("Unity play enter reported alreadyEntered after changing generations.playModeGeneration.");
        }

        return null;
    }

    private static bool IsReadyStoppedSnapshot (IpcUnityEditorObservation snapshot)
    {
        var state = snapshot.State;
        var playMode = state.PlayMode;
        return state.LifecycleState == IpcEditorLifecycleState.Ready
            && playMode.State == IpcPlayModeState.Stopped
            && playMode.Transition == IpcPlayModeTransition.None
            && !playMode.IsPlaying
            && !playMode.IsPlayingOrWillChangePlaymode;
    }

    private static bool IsEnteredSnapshot (IpcUnityEditorObservation snapshot)
    {
        var state = snapshot.State;
        var playMode = state.PlayMode;
        return state.LifecycleState == IpcEditorLifecycleState.PlayMode
            && playMode.State == IpcPlayModeState.Playing
            && playMode.Transition == IpcPlayModeTransition.None
            && playMode.IsPlaying;
    }

    private static ApplicationFailure CreateErrorFromUnityRequestFailure (UnityRequestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Code == ExecutionErrorCodes.IpcTimeout
            ? ApplicationFailure.Timeout(failure.Message, failure.Code)
            : ApplicationFailure.InternalError(failure.Message, failure.Code);
    }

    private static ApplicationFailure CreateErrorFromResponse (UnityRequestResponse response)
    {
        var firstError = response.Errors[0];
        var message = firstError.Message;
        var code = firstError.Code;
        if (code == PlayModeErrorCodes.PlayModeTransitionTimeout)
        {
            return ApplicationFailure.Timeout(message, code);
        }

        return ApplicationFailure.FromCode(code, message);
    }

    private static ApplicationFailure CreateErrorFromResponse (
        UnityRequestResponse response,
        PlayTransitionOutput transition)
    {
        var responseCode = response.Errors.FirstOrDefault()?.Code;
        var failure = CreateErrorFromResponse(response);
        if (responseCode is not null)
        {
            return failure;
        }

        return transition.Result == IpcPlayTransitionOutcome.Timeout
            ? ApplicationFailure.Timeout(failure.Message, PlayModeErrorCodes.PlayModeTransitionTimeout)
            : failure;
    }

    private static ApplicationFailure CreateTransitionFailure (
        UcliCode code,
        string message)
    {
        return code == PlayModeErrorCodes.PlayModeTransitionTimeout
            ? ApplicationFailure.Timeout(message, code)
            : ApplicationFailure.FromCode(code, message);
    }

    private static ApplicationFailure CreateStateUnknownFailure (string message)
    {
        return ApplicationFailure.InternalError(message, PlayModeErrorCodes.PlayModeStateUnknown);
    }

    private sealed record PlayEnterOutputCreationResult (
        PlayEnterExecutionOutput? Output,
        ApplicationFailure? Error)
    {
        public bool IsSuccess => Output is not null && Error is null;

        public static PlayEnterOutputCreationResult Success (PlayEnterExecutionOutput output)
        {
            return new PlayEnterOutputCreationResult(output, null);
        }

        public static PlayEnterOutputCreationResult Failure (ApplicationFailure error)
        {
            return new PlayEnterOutputCreationResult(null, error);
        }
    }
}
