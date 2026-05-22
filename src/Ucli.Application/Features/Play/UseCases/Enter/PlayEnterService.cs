using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Play.Common;
using MackySoft.Ucli.Application.Shared.CommandContracts.Projection;
using MackySoft.Ucli.Application.Shared.Execution;
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

        var response = executionResult.Response!;
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
        if (response.HasFailureStatus || response.Errors.Count != 0)
        {
            return PlayEnterExecutionResult.Failure(CreateErrorFromResponse(response, output.Transition), output);
        }

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
        if (!string.Equals(lifecycle.EditorMode, DaemonEditorModeValues.Gui, StringComparison.Ordinal))
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
            EditorMode: DaemonEditorModeValues.Gui,
            LifecycleState: lifecycle.LifecycleState,
            BlockingReason: lifecycle.BlockingReason,
            CompileState: lifecycle.CompileState,
            CompileGeneration: lifecycle.CompileGeneration,
            DomainReloadGeneration: lifecycle.DomainReloadGeneration,
            CanAcceptExecutionRequests: lifecycle.CanAcceptExecutionRequests,
            ObservedAtUtc: lifecycle.ObservedAtUtc,
            ActionRequired: lifecycle.ActionRequired,
            PrimaryDiagnostic: ToOutput(lifecycle.PrimaryDiagnostic),
            PlayMode: lifecycle.PlayMode,
            Transition: transition,
            TimeoutMilliseconds: context.TimeoutMilliseconds));
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

        return null;
    }

    private static bool IsEnteredSnapshot (IpcPlayLifecycleSnapshot snapshot)
    {
        var playMode = snapshot.PlayMode;
        return playMode is not null
            && string.Equals(snapshot.LifecycleState, IpcEditorLifecycleStateCodec.Playmode, StringComparison.Ordinal)
            && string.Equals(playMode.State, IpcPlayModeStateNames.Playing, StringComparison.Ordinal)
            && string.Equals(playMode.Transition, IpcPlayModeTransitionNames.None, StringComparison.Ordinal)
            && playMode.IsPlaying
            && !snapshot.CanAcceptExecutionRequests;
    }

    private static DaemonPrimaryDiagnosticOutput? ToOutput (IpcPrimaryDiagnostic? diagnostic)
    {
        if (diagnostic is null || !StringValueNormalizer.TryTrimToNonEmpty(diagnostic.Kind, out var kind))
        {
            return null;
        }

        return new DaemonPrimaryDiagnosticOutput(
            Kind: kind,
            Code: StringValueNormalizer.TrimToNull(diagnostic.Code),
            File: StringValueNormalizer.TrimToNull(diagnostic.File),
            Line: diagnostic.Line,
            Column: diagnostic.Column,
            Message: StringValueNormalizer.TrimToNull(diagnostic.Message));
    }

    private static ApplicationFailure CreateErrorFromUnityRequestFailure (UnityRequestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Code == ExecutionErrorCodes.IpcTimeout
            ? ApplicationFailure.Timeout(failure.Message, failure.Code)
            : ApplicationFailure.InternalError(failure.Message, failure.Code);
    }

    private static ApplicationFailure CreateErrorFromResponse (
        UnityRequestResponse response,
        IpcPlayTransitionResult transition)
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

        if (code.HasValue && code.Value.IsValid)
        {
            return ApplicationFailure.FromCode(code, message);
        }

        return transition.Result == IpcPlayTransitionResultNames.Timeout
            ? ApplicationFailure.Timeout(message, PlayModeErrorCodes.PlayModeTransitionTimeout)
            : ApplicationFailure.InternalError(message, code);
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
