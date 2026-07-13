using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.Common.Projection;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Exit;

/// <summary> Executes Play Mode exit against an existing registered GUI daemon session. </summary>
internal sealed class PlayExitService : IPlayExitService
{
    private const int ResponseGraceMilliseconds = 1000;
    private const string SessionNotAvailableMessage = "Registered GUI daemon session is not available for Play Mode exit.";
    private const string RequiresGuiEditorMessage = "Play Mode exit requires a registered GUI daemon session.";

    private readonly IPlayCommandExecutionContextResolver contextResolver;

    private readonly IUnityRequestExecutor unityRequestExecutor;

    /// <summary> Initializes a new instance of the <see cref="PlayExitService" /> class. </summary>
    /// <param name="contextResolver"> The Play Mode command context resolver dependency. </param>
    /// <param name="unityRequestExecutor"> The Unity IPC request executor dependency. </param>
    public PlayExitService (
        IPlayCommandExecutionContextResolver contextResolver,
        IUnityRequestExecutor unityRequestExecutor)
    {
        this.contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        this.unityRequestExecutor = unityRequestExecutor ?? throw new ArgumentNullException(nameof(unityRequestExecutor));
    }

    /// <inheritdoc />
    public async ValueTask<PlayExitExecutionResult> ExecuteAsync (
        PlayExitCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await contextResolver.ResolveAsync(
                input.ProjectPath,
                input.TimeoutMilliseconds,
                UcliCommandIds.PlayExit,
                SessionNotAvailableMessage,
                RequiresGuiEditorMessage,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            return PlayExitExecutionResult.Failure(contextResult.Error!);
        }

        var context = contextResult.Context!;
        var requestTimeout = context.Timeout + TimeSpan.FromMilliseconds(ResponseGraceMilliseconds);
        var executionResult = await unityRequestExecutor.ExecuteAsync(
                UcliCommandIds.PlayExit,
                UnityExecutionMode.Daemon,
                requestTimeout,
                context.ProjectContext.Config,
                context.ProjectContext.UnityProject,
                new UnityRequestPayload.PlayExit(context.TimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            return PlayExitExecutionResult.Failure(CreateErrorFromUnityRequestFailure(executionResult.FailureInfo!));
        }

        return CreateResultFromResponse(context, executionResult.Response!);
    }

    private PlayExitExecutionResult CreateResultFromResponse (
        PlayCommandExecutionContext context,
        UnityRequestResponse response)
    {
        if (response.HasFailureStatus || response.Errors.Count != 0)
        {
            if (!TryReadTransitionResponse(response, out var errorTransitionResponse, out _))
            {
                return PlayExitExecutionResult.Failure(CreateErrorFromResponse(response));
            }

            var errorOutputResult = CreateOutput(context, errorTransitionResponse!.Transition);
            if (!errorOutputResult.IsSuccess)
            {
                return PlayExitExecutionResult.Failure(errorOutputResult.Error!);
            }

            var errorOutput = errorOutputResult.Output!;
            return PlayExitExecutionResult.Failure(CreateErrorFromResponse(response, errorOutput.Transition), errorOutput);
        }

        if (!TryReadTransitionResponse(response, out var transitionResponse, out var payloadFailure))
        {
            return PlayExitExecutionResult.Failure(payloadFailure!);
        }

        var outputResult = CreateOutput(context, transitionResponse!.Transition);
        if (!outputResult.IsSuccess)
        {
            return PlayExitExecutionResult.Failure(outputResult.Error!);
        }

        var output = outputResult.Output!;
        return output.Transition.Result switch
        {
            IpcPlayTransitionResultNames.Exited or IpcPlayTransitionResultNames.AlreadyExited
                => PlayExitExecutionResult.Success(output),
            IpcPlayTransitionResultNames.Timeout
                => PlayExitExecutionResult.Failure(CreateTransitionFailure(
                    PlayModeErrorCodes.PlayModeTransitionTimeout,
                    $"Unity Play Mode exit timed out after {context.TimeoutMilliseconds} milliseconds."),
                    output),
            IpcPlayTransitionResultNames.Blocked
                => PlayExitExecutionResult.Failure(CreateTransitionFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    "Unity Play Mode exit was blocked by the current editor lifecycle state."),
                    output),
            _ => PlayExitExecutionResult.Failure(CreateStateUnknownFailure(
                $"Unity play exit returned unsupported result '{output.Transition.Result}'.")),
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
        failure = ApplicationFailure.InternalError($"Unity play exit payload is invalid. {payloadError.Message}");
        return false;
    }

    private static PlayExitOutputCreationResult CreateOutput (
        PlayCommandExecutionContext context,
        IpcPlayTransitionResult transition)
    {
        if (!string.Equals(transition.Transition, IpcPlayTransitionCommandNames.Exit, StringComparison.Ordinal))
        {
            return PlayExitOutputCreationResult.Failure(ApplicationFailure.InternalError(
                $"Unity play exit transition mismatch. Actual={transition.Transition}."));
        }

        if (transition.Before is null)
        {
            return PlayExitOutputCreationResult.Failure(CreateStateUnknownFailure("Unity play exit transition before snapshot is missing."));
        }

        var shapeFailure = ValidateTransitionShape(transition);
        if (shapeFailure is not null)
        {
            return PlayExitOutputCreationResult.Failure(shapeFailure);
        }

        var currentSnapshot = ResolveCurrentSnapshot(transition);
        if (currentSnapshot is null)
        {
            return PlayExitOutputCreationResult.Failure(CreateStateUnknownFailure("Unity play exit current lifecycle snapshot is missing."));
        }

        var validationFailure = ValidateTransitionSnapshots(context, transition, currentSnapshot);
        if (validationFailure is not null)
        {
            return PlayExitOutputCreationResult.Failure(validationFailure);
        }

        var lifecycle = PlayOutputProjectionFactory.CreateSnapshotOutput(currentSnapshot);
        if (lifecycle.EditorMode != DaemonEditorMode.Gui)
        {
            return PlayExitOutputCreationResult.Failure(ApplicationFailure.InternalError(
                RequiresGuiEditorMessage,
                PlayModeErrorCodes.PlayModeRequiresGuiEditor));
        }

        return PlayExitOutputCreationResult.Success(new PlayExitExecutionOutput(
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
            Transition: CreateTransitionOutput(transition),
            TimeoutMilliseconds: context.TimeoutMilliseconds));
    }

    private static ApplicationFailure? ValidateTransitionShape (IpcPlayTransitionResult transition)
    {
        return transition.Result switch
        {
            IpcPlayTransitionResultNames.Exited or IpcPlayTransitionResultNames.AlreadyExited
                => transition.After is null
                    ? CreateStateUnknownFailure("Unity play exit success omitted the after snapshot.")
                    : transition.Observed is not null || !string.IsNullOrWhiteSpace(transition.ApplicationState)
                        ? CreateStateUnknownFailure("Unity play exit success included transition error fields.")
                        : null,
            IpcPlayTransitionResultNames.Timeout or IpcPlayTransitionResultNames.Blocked => ValidateTransitionErrorShape(transition),
            _ => CreateStateUnknownFailure($"Unity play exit returned unsupported result '{transition.Result}'."),
        };
    }

    private static ApplicationFailure? ValidateTransitionErrorShape (IpcPlayTransitionResult transition)
    {
        if (transition.After is not null)
        {
            return CreateStateUnknownFailure("Unity play exit transition error included the after snapshot.");
        }

        return IsValidApplicationState(transition.ApplicationState)
            ? null
            : CreateStateUnknownFailure($"Unity play exit transition error applicationState is invalid. Actual={transition.ApplicationState}.");
    }

    private static IpcUnityEditorObservation? ResolveCurrentSnapshot (IpcPlayTransitionResult transition)
    {
        return transition.Result switch
        {
            IpcPlayTransitionResultNames.Exited or IpcPlayTransitionResultNames.AlreadyExited => transition.After,
            IpcPlayTransitionResultNames.Timeout or IpcPlayTransitionResultNames.Blocked => transition.Observed,
            _ => null,
        };
    }

    private static PlayExitTransitionOutput CreateTransitionOutput (IpcPlayTransitionResult transition)
    {
        return new PlayExitTransitionOutput(
            Transition: transition.Transition,
            Result: transition.Result,
            Before: PlayOutputProjectionFactory.CreateSnapshotOutput(transition.Before),
            After: transition.After is null ? null : PlayOutputProjectionFactory.CreateSnapshotOutput(transition.After),
            Observed: transition.Observed is null ? null : PlayOutputProjectionFactory.CreateSnapshotOutput(transition.Observed),
            ApplicationState: transition.ApplicationState);
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
            IpcPlayTransitionResultNames.Exited => ValidateExited(transition.Before, currentSnapshot),
            IpcPlayTransitionResultNames.AlreadyExited => ValidateAlreadyExited(transition.Before, currentSnapshot),
            IpcPlayTransitionResultNames.Timeout => ValidateErrorTransition(transition, IpcPlayApplicationStateNames.Indeterminate),
            IpcPlayTransitionResultNames.Blocked => ValidateErrorTransition(transition, expectedApplicationState: null),
            _ => CreateStateUnknownFailure($"Unity play exit returned unsupported result '{transition.Result}'."),
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
                $"Unity play exit {label} projectFingerprint mismatch. Requested={context.Project.ProjectFingerprint}, Actual={snapshot.ProjectFingerprint}.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateExited (
        IpcUnityEditorObservation before,
        IpcUnityEditorObservation after)
    {
        if (!IsEnteredSnapshot(before))
        {
            return CreateStateUnknownFailure("Unity play exit reported exited without a playing before snapshot.");
        }

        if (!IsReadyStoppedSnapshot(after))
        {
            return CreateStateUnknownFailure("Unity play exit reported exited without a ready stopped snapshot.");
        }

        if (before.State.Generations.PlayModeGeneration == after.State.Generations.PlayModeGeneration)
        {
            return CreateStateUnknownFailure("Unity play exit reported exited without changing generations.playModeGeneration.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateAlreadyExited (
        IpcUnityEditorObservation before,
        IpcUnityEditorObservation after)
    {
        if (!IsStoppedPlayModeSnapshot(before) || !IsStoppedPlayModeSnapshot(after))
        {
            return CreateStateUnknownFailure("Unity play exit reported alreadyExited without a stopped snapshot.");
        }

        if (before.State.Generations.PlayModeGeneration != after.State.Generations.PlayModeGeneration)
        {
            return CreateStateUnknownFailure("Unity play exit reported alreadyExited after changing generations.playModeGeneration.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateErrorTransition (
        IpcPlayTransitionResult transition,
        string? expectedApplicationState)
    {
        if (transition.Observed is null)
        {
            return CreateStateUnknownFailure("Unity play exit transition error omitted the observed snapshot.");
        }

        if (expectedApplicationState is not null
            && !string.Equals(transition.ApplicationState, expectedApplicationState, StringComparison.Ordinal))
        {
            return CreateStateUnknownFailure(
                $"Unity play exit transition error applicationState mismatch. Expected={expectedApplicationState}, Actual={transition.ApplicationState}.");
        }

        if (string.IsNullOrWhiteSpace(transition.ApplicationState))
        {
            return CreateStateUnknownFailure("Unity play exit transition error omitted applicationState.");
        }

        if (!IsValidApplicationState(transition.ApplicationState))
        {
            return CreateStateUnknownFailure(
                $"Unity play exit transition error applicationState is invalid. Actual={transition.ApplicationState}.");
        }

        return null;
    }

    private static bool IsValidApplicationState (string? applicationState)
    {
        return applicationState is IpcPlayApplicationStateNames.NotApplied
            or IpcPlayApplicationStateNames.Applied
            or IpcPlayApplicationStateNames.Indeterminate
            or IpcPlayApplicationStateNames.Unknown;
    }

    private static bool IsReadyStoppedSnapshot (IpcUnityEditorObservation snapshot)
    {
        return IsStoppedPlayModeSnapshot(snapshot)
            && snapshot.State.LifecycleState == IpcEditorLifecycleState.Ready;
    }

    private static bool IsStoppedPlayModeSnapshot (IpcUnityEditorObservation snapshot)
    {
        var playMode = snapshot.State.PlayMode;
        return playMode.State == IpcPlayModeState.Stopped
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
        var firstError = response.Errors.FirstOrDefault();
        var message = string.IsNullOrWhiteSpace(firstError?.Message)
            ? $"Unity play exit IPC failed with status '{response.FailureStatus}'."
            : firstError!.Message;
        var code = firstError?.Code;
        if (code == PlayModeErrorCodes.PlayModeTransitionTimeout)
        {
            return ApplicationFailure.Timeout(message, code);
        }

        return code.HasValue && code.Value.IsValid
            ? ApplicationFailure.FromCode(code, message)
            : ApplicationFailure.InternalError(message, code);
    }

    private static ApplicationFailure CreateErrorFromResponse (
        UnityRequestResponse response,
        PlayExitTransitionOutput transition)
    {
        var responseCode = response.Errors.FirstOrDefault()?.Code;
        var failure = CreateErrorFromResponse(response);
        if (responseCode.HasValue && responseCode.Value.IsValid)
        {
            return failure;
        }

        return transition.Result == IpcPlayTransitionResultNames.Timeout
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

    private sealed record PlayExitOutputCreationResult (
        PlayExitExecutionOutput? Output,
        ApplicationFailure? Error)
    {
        public bool IsSuccess => Output is not null && Error is null;

        public static PlayExitOutputCreationResult Success (PlayExitExecutionOutput output)
        {
            return new PlayExitOutputCreationResult(output, null);
        }

        public static PlayExitOutputCreationResult Failure (ApplicationFailure error)
        {
            return new PlayExitOutputCreationResult(null, error);
        }
    }
}
