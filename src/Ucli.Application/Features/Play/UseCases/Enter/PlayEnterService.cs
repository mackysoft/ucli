using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Features.Play.Common.Projection;
using MackySoft.Ucli.Application.Shared.CommandContracts.Projection;
using MackySoft.Ucli.Contracts.Ipc;

using MackySoft.Ucli.Contracts.Text;

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
                new UnityRequestPayload.PlayEnter(context.TimeoutMilliseconds),
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
        if (response.HasFailureStatus || response.Errors.Count != 0)
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
            IpcPlayTransitionResultNames.Entered or IpcPlayTransitionResultNames.AlreadyEntered
                => PlayEnterExecutionResult.Success(output),
            IpcPlayTransitionResultNames.Timeout
                => PlayEnterExecutionResult.Failure(CreateTransitionFailure(
                    PlayModeErrorCodes.PlayModeTransitionTimeout,
                    $"Unity Play Mode enter timed out after {context.TimeoutMilliseconds} milliseconds."),
                    output),
            IpcPlayTransitionResultNames.Blocked
                => PlayEnterExecutionResult.Failure(CreateTransitionFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    "Unity Play Mode enter was blocked by the current editor lifecycle state."),
                    output),
            _ => PlayEnterExecutionResult.Failure(CreateStateUnknownFailure(
                $"Unity play enter returned unsupported result '{output.Transition.Result}'.")),
        };
    }

    private static bool TryReadTransitionResponse (
        UnityRequestResponse response,
        out IpcPlayTransitionResponse? transitionResponse,
        out ApplicationFailure? failure)
    {
        if (IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPlayTransitionResponse payload, out var payloadError))
        {
            if (payload.Transition is null)
            {
                transitionResponse = null;
                failure = CreateStateUnknownFailure("Unity play enter transition payload is missing.");
                return false;
            }

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
        if (!string.Equals(transition.Transition, IpcPlayTransitionCommandNames.Enter, StringComparison.Ordinal))
        {
            return PlayEnterOutputCreationResult.Failure(ApplicationFailure.InternalError(
                $"Unity play enter transition mismatch. Actual={transition.Transition}."));
        }

        if (transition.Before is null)
        {
            return PlayEnterOutputCreationResult.Failure(CreateStateUnknownFailure("Unity play enter transition before snapshot is missing."));
        }

        var shapeFailure = ValidateTransitionShape(transition);
        if (shapeFailure is not null)
        {
            return PlayEnterOutputCreationResult.Failure(shapeFailure);
        }

        var currentSnapshot = ResolveCurrentSnapshot(transition);
        if (currentSnapshot is null)
        {
            return PlayEnterOutputCreationResult.Failure(CreateStateUnknownFailure("Unity play enter current lifecycle snapshot is missing."));
        }

        var validationFailure = ValidateTransitionSnapshots(context, transition, currentSnapshot);
        if (validationFailure is not null)
        {
            return PlayEnterOutputCreationResult.Failure(validationFailure);
        }

        var lifecycle = LifecycleProjectionFactory.Create(currentSnapshot);
        if (!ContractLiteralCodec.Matches(lifecycle.EditorMode, DaemonEditorMode.Gui))
        {
            return PlayEnterOutputCreationResult.Failure(ApplicationFailure.InternalError(
                RequiresGuiEditorMessage,
                PlayModeErrorCodes.PlayModeRequiresGuiEditor));
        }

        if (lifecycle.PlayMode is null)
        {
            return PlayEnterOutputCreationResult.Failure(CreateStateUnknownFailure("Unity play enter playMode snapshot is missing or invalid."));
        }

        return PlayEnterOutputCreationResult.Success(new PlayEnterExecutionOutput(
            Project: context.Project,
            DaemonStatus: DaemonStatusKind.Running,
            ServerVersion: lifecycle.ServerVersion,
            EditorMode: ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
            LifecycleState: lifecycle.LifecycleState.HasValue
                ? ContractLiteralCodec.ToValue(lifecycle.LifecycleState.Value)
                : null,
            BlockingReason: lifecycle.BlockingReason.HasValue
                ? ContractLiteralCodec.ToValue(lifecycle.BlockingReason.Value)
                : null,
            CompileState: lifecycle.CompileState.HasValue
                ? ContractLiteralCodec.ToValue(lifecycle.CompileState.Value)
                : null,
            CompileGeneration: lifecycle.CompileGeneration,
            DomainReloadGeneration: lifecycle.DomainReloadGeneration,
            CanAcceptExecutionRequests: lifecycle.CanAcceptExecutionRequests,
            ObservedAtUtc: lifecycle.ObservedAtUtc,
            ActionRequired: lifecycle.ActionRequired,
            PrimaryDiagnostic: PlayOutputProjectionFactory.CreatePrimaryDiagnosticOutput(lifecycle.PrimaryDiagnostic),
            PlayMode: lifecycle.PlayMode,
            Transition: CreateTransitionOutput(transition),
            TimeoutMilliseconds: context.TimeoutMilliseconds));
    }

    private static ApplicationFailure? ValidateTransitionShape (IpcPlayTransitionResult transition)
    {
        return transition.Result switch
        {
            IpcPlayTransitionResultNames.Entered or IpcPlayTransitionResultNames.AlreadyEntered
                => transition.After is null
                    ? CreateStateUnknownFailure("Unity play enter success omitted the after snapshot.")
                    : transition.Observed is not null || !string.IsNullOrWhiteSpace(transition.ApplicationState)
                        ? CreateStateUnknownFailure("Unity play enter success included transition error fields.")
                        : null,
            IpcPlayTransitionResultNames.Timeout or IpcPlayTransitionResultNames.Blocked => ValidateTransitionErrorShape(transition),
            _ => CreateStateUnknownFailure($"Unity play enter returned unsupported result '{transition.Result}'."),
        };
    }

    private static ApplicationFailure? ValidateTransitionErrorShape (IpcPlayTransitionResult transition)
    {
        if (transition.After is not null)
        {
            return CreateStateUnknownFailure("Unity play enter transition error included the after snapshot.");
        }

        return IsValidApplicationState(transition.ApplicationState)
            ? null
            : CreateStateUnknownFailure($"Unity play enter transition error applicationState is invalid. Actual={transition.ApplicationState}.");
    }

    private static IpcPlayLifecycleSnapshot? ResolveCurrentSnapshot (IpcPlayTransitionResult transition)
    {
        return transition.Result switch
        {
            IpcPlayTransitionResultNames.Entered or IpcPlayTransitionResultNames.AlreadyEntered => transition.After,
            IpcPlayTransitionResultNames.Timeout or IpcPlayTransitionResultNames.Blocked => transition.Observed,
            _ => null,
        };
    }

    private static PlayEnterTransitionOutput CreateTransitionOutput (IpcPlayTransitionResult transition)
    {
        return new PlayEnterTransitionOutput(
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
        IpcPlayLifecycleSnapshot currentSnapshot)
    {
        var beforeFailure = ValidateSnapshotProjectAndPlayMode(context, transition.Before, "before");
        if (beforeFailure is not null)
        {
            return beforeFailure;
        }

        var currentFailure = ValidateSnapshotProjectAndPlayMode(context, currentSnapshot, "current");
        if (currentFailure is not null)
        {
            return currentFailure;
        }

        return transition.Result switch
        {
            IpcPlayTransitionResultNames.Entered => ValidateEntered(transition.Before, currentSnapshot),
            IpcPlayTransitionResultNames.AlreadyEntered => ValidateAlreadyEntered(transition.Before, currentSnapshot),
            IpcPlayTransitionResultNames.Timeout => ValidateErrorTransition(transition, IpcPlayApplicationStateNames.Indeterminate),
            IpcPlayTransitionResultNames.Blocked => ValidateErrorTransition(transition, expectedApplicationState: null),
            _ => CreateStateUnknownFailure($"Unity play enter returned unsupported result '{transition.Result}'."),
        };
    }

    private static ApplicationFailure? ValidateSnapshotProjectAndPlayMode (
        PlayCommandExecutionContext context,
        IpcPlayLifecycleSnapshot snapshot,
        string label)
    {
        if (!string.Equals(snapshot.ProjectFingerprint, context.Project.ProjectFingerprint, StringComparison.Ordinal))
        {
            return ApplicationFailure.InternalError(
                $"Unity play enter {label} projectFingerprint mismatch. Requested={context.Project.ProjectFingerprint}, Actual={snapshot.ProjectFingerprint}.");
        }

        return PlayModeSnapshotOutputFactory.Create(snapshot.PlayMode) is null
            ? CreateStateUnknownFailure($"Unity play enter {label} playMode snapshot is missing or invalid.")
            : null;
    }

    private static ApplicationFailure? ValidateEntered (
        IpcPlayLifecycleSnapshot before,
        IpcPlayLifecycleSnapshot after)
    {
        if (!IsReadyStoppedSnapshot(before))
        {
            return CreateStateUnknownFailure("Unity play enter reported entered without a ready stopped before snapshot.");
        }

        if (!IsEnteredSnapshot(after))
        {
            return CreateStateUnknownFailure("Unity play enter reported entered without a playing snapshot.");
        }

        if (string.Equals(before.PlayMode?.Generation, after.PlayMode?.Generation, StringComparison.Ordinal))
        {
            return CreateStateUnknownFailure("Unity play enter reported entered without changing playMode.generation.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateAlreadyEntered (
        IpcPlayLifecycleSnapshot before,
        IpcPlayLifecycleSnapshot after)
    {
        if (!IsEnteredSnapshot(before) || !IsEnteredSnapshot(after))
        {
            return CreateStateUnknownFailure("Unity play enter reported alreadyEntered without a playing snapshot.");
        }

        if (!string.Equals(before.PlayMode?.Generation, after.PlayMode?.Generation, StringComparison.Ordinal))
        {
            return CreateStateUnknownFailure("Unity play enter reported alreadyEntered after changing playMode.generation.");
        }

        return null;
    }

    private static ApplicationFailure? ValidateErrorTransition (
        IpcPlayTransitionResult transition,
        string? expectedApplicationState)
    {
        if (transition.Observed is null)
        {
            return CreateStateUnknownFailure("Unity play enter transition error omitted the observed snapshot.");
        }

        if (expectedApplicationState is not null
            && !string.Equals(transition.ApplicationState, expectedApplicationState, StringComparison.Ordinal))
        {
            return CreateStateUnknownFailure(
                $"Unity play enter transition error applicationState mismatch. Expected={expectedApplicationState}, Actual={transition.ApplicationState}.");
        }

        if (string.IsNullOrWhiteSpace(transition.ApplicationState))
        {
            return CreateStateUnknownFailure("Unity play enter transition error omitted applicationState.");
        }

        if (!IsValidApplicationState(transition.ApplicationState))
        {
            return CreateStateUnknownFailure(
                $"Unity play enter transition error applicationState is invalid. Actual={transition.ApplicationState}.");
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

    private static bool IsReadyStoppedSnapshot (IpcPlayLifecycleSnapshot snapshot)
    {
        return TryReadPlayModeSnapshot(
                snapshot,
                out var playMode,
                out var playModeState,
                out var playModeTransition)
            && ContractLiteralCodec.Matches(snapshot.LifecycleState, IpcEditorLifecycleState.Ready)
            && string.IsNullOrWhiteSpace(snapshot.BlockingReason)
            && snapshot.CanAcceptExecutionRequests
            && playModeState == IpcPlayModeState.Stopped
            && playModeTransition == IpcPlayModeTransition.None
            && !playMode.IsPlaying
            && !playMode.IsPlayingOrWillChangePlaymode;
    }

    private static bool IsEnteredSnapshot (IpcPlayLifecycleSnapshot snapshot)
    {
        return TryReadPlayModeSnapshot(
                snapshot,
                out var playMode,
                out var playModeState,
                out var playModeTransition)
            && ContractLiteralCodec.Matches(snapshot.LifecycleState, IpcEditorLifecycleState.PlayMode)
            && ContractLiteralCodec.Matches(snapshot.BlockingReason, IpcEditorBlockingReason.PlayMode)
            && playModeState == IpcPlayModeState.Playing
            && playModeTransition == IpcPlayModeTransition.None
            && playMode.IsPlaying
            && !snapshot.CanAcceptExecutionRequests;
    }

    private static bool TryReadPlayModeSnapshot (
        IpcPlayLifecycleSnapshot snapshot,
        out IpcPlayModeSnapshot playMode,
        out IpcPlayModeState state,
        out IpcPlayModeTransition transition)
    {
        playMode = snapshot.PlayMode!;
        state = default;
        transition = default;
        return playMode is not null
            && ContractLiteralInputParser.TryParseTrimmed<IpcPlayModeState>(playMode.State, out state)
            && ContractLiteralInputParser.TryParseTrimmed<IpcPlayModeTransition>(playMode.Transition, out transition);
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
            ? $"Unity play enter IPC failed with status '{response.FailureStatus}'."
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
        PlayEnterTransitionOutput transition)
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
