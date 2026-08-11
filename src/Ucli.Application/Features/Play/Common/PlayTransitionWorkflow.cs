using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Features.Play.Common.Projection;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Play.Common;

/// <summary>
/// Coordinates Play-specific context resolution, IPC delivery, reconnection, and result
/// projection while delegating common registration issuance and direction-specific state
/// interpretation to their owning policies.
/// </summary>
internal sealed partial class PlayTransitionWorkflow
{
    private readonly ILifecycleExecutionReconnectResolver reconnectResolver;

    private readonly ILifecycleExecutionHostExitTerminalizer hostExitTerminalizer;

    private readonly LifecycleExecutionRegistrationIssuer registrationIssuer;

    private readonly TimeProvider timeProvider;

    public PlayTransitionWorkflow (
        ILifecycleExecutionReconnectResolver reconnectResolver,
        ILifecycleExecutionHostExitTerminalizer hostExitTerminalizer,
        LifecycleExecutionRegistrationIssuer registrationIssuer,
        TimeProvider timeProvider)
    {
        this.reconnectResolver = reconnectResolver
            ?? throw new ArgumentNullException(nameof(reconnectResolver));
        this.hostExitTerminalizer = hostExitTerminalizer
            ?? throw new ArgumentNullException(nameof(hostExitTerminalizer));
        this.registrationIssuer = registrationIssuer
            ?? throw new ArgumentNullException(nameof(registrationIssuer));
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    private async ValueTask<PlayTransitionWorkflowResult<TOutput>>
        ExecuteRegisteredAsync<TOutput> (
            PlayCommandExecutionContext context,
            LifecycleExecutionRegistration registration,
            ExecutionRef? establishedExecutionReference,
            LifecycleExecutionStartBinding? requiredStart,
            IPlayTransitionDirectionPolicy<TOutput> direction,
            CancellationToken cancellationToken,
            Func<UnityRequestPayload, CancellationToken, ValueTask<UnityRequestExecutionResult>> dispatchAsync)
        where TOutput : class
    {
        var payload = direction.CreatePayload(registration, requiredStart);
        var executionResult = await dispatchAsync(payload, cancellationToken)
            .ConfigureAwait(false);
        if (!executionResult.IsSuccess)
        {
            return executionResult.ConfirmedHostExit is not null
                ? await TerminalizeConfirmedHostExitAsync(
                        context,
                        establishedExecutionReference,
                        executionResult,
                        direction)
                    .ConfigureAwait(false)
                : CreateWaitFailure<TOutput>(
                    context,
                    establishedExecutionReference,
                    executionResult);
        }

        return CreateResultFromResponse(
            context,
            registration,
            executionResult,
            direction);
    }

    private async ValueTask<PlayTransitionWorkflowResult<TOutput>>
        TerminalizeConfirmedHostExitAsync<TOutput> (
            PlayCommandExecutionContext context,
            ExecutionRef? establishedExecutionReference,
            UnityRequestExecutionResult executionResult,
            IPlayTransitionDirectionPolicy<TOutput> direction)
        where TOutput : class
    {
        var start = executionResult.LifecycleExecutionStart!;
        var currentReference =
            establishedExecutionReference
            ?? start.LifecycleExecutionRef;
        var terminalFacts =
            LifecycleExecutionTerminalFactsPolicy.ResolveHostExit(
                start,
                currentReference,
                executionResult.LifecycleActionDispatched,
                timeProvider.GetUtcNow());
        var terminalization =
            await hostExitTerminalizer.TerminalizeAsync(
                    context.ProjectContext.UnityProject,
                    start,
                    currentReference,
                    terminalFacts,
                    direction.CreateHostExitTerminalRecord)
                .ConfigureAwait(false);
        if (terminalization
            is LifecycleExecutionHostExitTerminalizationResult
                .PublicationFailed publicationFailed)
        {
            var failureContext = CreateFailureContext(
                context,
                publicationFailed.ExecutionReference,
                publicationFailed.ApplicationState);
            if (publicationFailed.FixedTerminalRecord is not null
                && direction.TryGetTerminalResult(
                    publicationFailed.FixedTerminalRecord,
                    out var fixedTransition)
                && fixedTransition is not null
                && TryCreateTypedFailureContext(
                    context,
                    publicationFailed.ExecutionReference,
                    publicationFailed.ApplicationState,
                    fixedTransition,
                    direction,
                    out var typedFailureContext,
                    out _))
            {
                failureContext = typedFailureContext;
            }

            return PlayTransitionWorkflowResult<TOutput>.Failure(
                publicationFailed.Failure,
                failureContext);
        }

        var published =
            (LifecycleExecutionHostExitTerminalizationResult.Published)
                terminalization;
        return CreateResultFromTerminalRecord(
            context,
            published.ExecutionReference,
            published.TerminalRecord,
            direction);
    }

    private static PlayTransitionWorkflowResult<TOutput> CreateWaitFailure<TOutput> (
        PlayCommandExecutionContext context,
        ExecutionRef? establishedExecutionReference,
        UnityRequestExecutionResult executionResult)
        where TOutput : class
    {
        var waitFailure = LifecycleExecutionWaitFailure.Resolve(
            durableStartExecutionReference: executionResult
                .LifecycleExecutionStart
                ?.LifecycleExecutionRef,
            isCallerCancellation: executionResult
                .FailureInfo!.Code
                == ExecutionErrorCodes.Canceled,
            lifecycleActionDispatched:
                executionResult.LifecycleActionDispatched,
            establishedExecutionReference:
                establishedExecutionReference);
        return PlayTransitionWorkflowResult<TOutput>.Failure(
            CreateErrorFromUnityRequestFailure(executionResult.FailureInfo!),
            CreateFailureContext(
                context,
                waitFailure.ExecutionReference,
                waitFailure.ApplicationState));
    }

    private static PlayTransitionWorkflowResult<TOutput>
        CreateResultFromTerminalRecord<TOutput> (
            PlayCommandExecutionContext context,
            ExecutionRef executionReference,
            LifecycleExecutionTerminalRecord terminalRecord,
            IPlayTransitionDirectionPolicy<TOutput> direction)
        where TOutput : class
    {
        if (executionReference is not TerminalExecutionRef terminalReference
            || !direction.TryGetTerminalResult(
                terminalRecord,
                out var transition))
        {
            return PlayTransitionWorkflowResult<TOutput>.Failure(
                ApplicationFailure.InternalError(
                    $"{direction.CommandDisplayName} reconnection did not resolve its typed Terminal Record."),
                CreateFailureContext(
                    context,
                    executionReference,
                    ExecutionApplicationState.Indeterminate));
        }

        if (terminalRecord.TerminalReason
            == LifecycleExecutionTerminalReason.Completed)
        {
            return TryCreateOutput(
                    context,
                    terminalReference,
                    transition!,
                    direction,
                    out var output,
                    out var outputFailure)
                ? PlayTransitionWorkflowResult<TOutput>.Success(output!)
                : PlayTransitionWorkflowResult<TOutput>.Failure(
                    outputFailure!,
                    CreateFailureContext(
                        context,
                        terminalReference,
                        terminalRecord.ApplicationState));
        }

        var failure = CreateTerminalFailure(
            terminalRecord.TerminalReason,
            direction);
        if (transition is null)
        {
            return PlayTransitionWorkflowResult<TOutput>.Failure(
                failure,
                CreateFailureContext(
                    context,
                    terminalReference,
                    terminalRecord.ApplicationState));
        }

        return TryCreateTypedFailureContext(
                context,
                terminalReference,
                terminalRecord.ApplicationState,
                transition,
                direction,
                out var failureContext,
                out var failureContextError)
            ? PlayTransitionWorkflowResult<TOutput>.Failure(
                failure,
                failureContext)
            : PlayTransitionWorkflowResult<TOutput>.Failure(
                failureContextError!,
                CreateFailureContext(
                    context,
                    terminalReference,
                    terminalRecord.ApplicationState));
    }

    private static ApplicationFailure CreateTerminalFailure<TOutput> (
        LifecycleExecutionTerminalReason terminalReason,
        IPlayTransitionDirectionPolicy<TOutput> direction)
        where TOutput : class
    {
        return terminalReason switch
        {
            LifecycleExecutionTerminalReason.ActionFailed =>
                ApplicationFailure.ContractViolation(
                    $"{direction.ActionDisplayName} ended with an explicit action failure.",
                    direction.ActionRejectedCode),
            LifecycleExecutionTerminalReason.DeadlineExceeded =>
                ApplicationFailure.Timeout(
                    $"{direction.ActionDisplayName} reached its durable execution deadline.",
                    LifecycleExecutionErrorCodes.DeadlineExceeded),
            LifecycleExecutionTerminalReason.ProjectMismatch =>
                ApplicationFailure.ContractViolation(
                    $"{direction.ActionDisplayName} recovery project does not match its durable start.",
                    LifecycleExecutionErrorCodes.ProjectMismatch),
            LifecycleExecutionTerminalReason.HostMismatch =>
                ApplicationFailure.ContractViolation(
                    $"{direction.ActionDisplayName} recovery host does not match its durable start.",
                    LifecycleExecutionErrorCodes.HostMismatch),
            LifecycleExecutionTerminalReason.GenerationMismatch =>
                ApplicationFailure.ContractViolation(
                    $"{direction.ActionDisplayName} recovery generation was not a proven successor.",
                    LifecycleExecutionErrorCodes.GenerationMismatch),
            LifecycleExecutionTerminalReason.UnityExited =>
                ApplicationFailure.ExternalProcessFailure(
                    $"The Unity Editor hosting {direction.ActionDisplayName} exited before completion.",
                    LifecycleExecutionErrorCodes.UnityExited),
            _ => throw new ArgumentOutOfRangeException(
                nameof(terminalReason),
                terminalReason,
                $"Completed {direction.ActionDisplayName} Terminal Records are projected as success."),
        };
    }

    private static PlayTransitionWorkflowResult<TOutput>
        CreateResultFromResponse<TOutput> (
            PlayCommandExecutionContext context,
            LifecycleExecutionRegistration registration,
            UnityRequestExecutionResult executionResult,
            IPlayTransitionDirectionPolicy<TOutput> direction)
        where TOutput : class
    {
        var response = executionResult.Response!;
        if (response.Errors.Count != 0
            && executionResult.LifecycleExecutionStart is null)
        {
            return PlayTransitionWorkflowResult<TOutput>.Failure(
                CreateErrorFromResponse(response));
        }

        if (response.Errors.Count != 0)
        {
            return CreateResultFromErrorResponse(
                context,
                registration,
                executionResult,
                direction);
        }

        if (!TryReadTransitionResponse(
                response,
                direction,
                out var transitionResponse,
                out var payloadFailure))
        {
            return PlayTransitionWorkflowResult<TOutput>.Failure(
                payloadFailure!,
                CreateFailureContext(
                    context,
                    executionResult.LifecycleExecutionStart));
        }

        if (!registration.HasSameIdentity(
                transitionResponse!.LifecycleExecutionRef))
        {
            return CreateUntrustedResponseFailure<TOutput>(
                context,
                executionResult.LifecycleExecutionStart,
                $"Unity {GetProtocolActionName(direction)} response identifies a different Lifecycle Execution.");
        }

        if (!TryCreateOutput(
                context,
                transitionResponse.LifecycleExecutionRef,
                transitionResponse.Result,
                direction,
                out var output,
                out var outputFailure))
        {
            return PlayTransitionWorkflowResult<TOutput>.Failure(
                outputFailure!,
                CreateFailureContext(
                    context,
                    executionResult.LifecycleExecutionStart));
        }

        if (!direction.IsSuccessfulOutcome(
                transitionResponse.Result.Result))
        {
            throw new ArgumentOutOfRangeException(
                nameof(PlayTransitionOutput.Result),
                transitionResponse.Result.Result,
                null);
        }

        return PlayTransitionWorkflowResult<TOutput>.Success(output!);
    }

    private static PlayTransitionWorkflowResult<TOutput>
        CreateResultFromErrorResponse<TOutput> (
            PlayCommandExecutionContext context,
            LifecycleExecutionRegistration registration,
            UnityRequestExecutionResult executionResult,
            IPlayTransitionDirectionPolicy<TOutput> direction)
        where TOutput : class
    {
        var response = executionResult.Response!;
        if (!TryReadErrorTransitionResponse(
                response,
                direction,
                out var errorTransitionResponse,
                out var errorPayloadFailure))
        {
            return PlayTransitionWorkflowResult<TOutput>.Failure(
                errorPayloadFailure!,
                CreateFailureContext(
                    context,
                    executionResult.LifecycleExecutionStart));
        }

        if (errorTransitionResponse!.LifecycleExecutionRef == null
            || !registration.HasSameIdentity(
                errorTransitionResponse.LifecycleExecutionRef))
        {
            return CreateUntrustedResponseFailure<TOutput>(
                context,
                executionResult.LifecycleExecutionStart,
                $"Unity {GetProtocolActionName(direction)} error response identifies a different Lifecycle Execution.");
        }

        var errorFailureContext = CreateFailureContext(
            context,
            errorTransitionResponse.LifecycleExecutionRef,
            errorTransitionResponse.ApplicationState);
        if (errorTransitionResponse.Result == null)
        {
            return PlayTransitionWorkflowResult<TOutput>.Failure(
                CreateErrorFromResponse(response),
                errorFailureContext);
        }

        if (!TryCreateTypedResponseFailure(
                response,
                errorTransitionResponse.LifecycleExecutionRef,
                errorTransitionResponse.Result,
                direction,
                out var typedResponseFailure))
        {
            return PlayTransitionWorkflowResult<TOutput>.Failure(
                typedResponseFailure!,
                CreateFailureContext(
                    context,
                    executionResult.LifecycleExecutionStart));
        }

        return TryCreateTypedFailureContext(
                context,
                errorTransitionResponse.LifecycleExecutionRef,
                errorTransitionResponse.ApplicationState,
                errorTransitionResponse.Result,
                direction,
                out var failureContext,
                out var failureContextError)
            ? PlayTransitionWorkflowResult<TOutput>.Failure(
                typedResponseFailure!,
                failureContext)
            : PlayTransitionWorkflowResult<TOutput>.Failure(
                failureContextError!,
                CreateFailureContext(
                    context,
                    executionResult.LifecycleExecutionStart));
    }

    private static PlayTransitionWorkflowResult<TOutput>
        CreateUntrustedResponseFailure<TOutput> (
            PlayCommandExecutionContext context,
            LifecycleExecutionStartBinding? start,
            string message)
        where TOutput : class
    {
        return PlayTransitionWorkflowResult<TOutput>.Failure(
            ApplicationFailure.InternalError(message),
            CreateFailureContext(context, start));
    }

    private static bool TryReadErrorTransitionResponse<TOutput> (
        UnityRequestResponse response,
        IPlayTransitionDirectionPolicy<TOutput> direction,
        out IpcPlayTransitionErrorResponse? transitionResponse,
        out ApplicationFailure? failure)
        where TOutput : class
    {
        if (IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcPlayTransitionErrorResponse payload,
                out var payloadError))
        {
            transitionResponse = payload;
            failure = null;
            return true;
        }

        transitionResponse = null;
        failure = ApplicationFailure.InternalError(
            $"Unity {GetProtocolActionName(direction)} error payload is invalid. {payloadError.Message}");
        return false;
    }

    private static bool TryReadTransitionResponse<TOutput> (
        UnityRequestResponse response,
        IPlayTransitionDirectionPolicy<TOutput> direction,
        out IpcPlayTransitionResponse? transitionResponse,
        out ApplicationFailure? failure)
        where TOutput : class
    {
        if (IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcPlayTransitionResponse payload,
                out var payloadError))
        {
            transitionResponse = payload;
            failure = null;
            return true;
        }

        transitionResponse = null;
        failure = ApplicationFailure.InternalError(
            $"Unity {GetProtocolActionName(direction)} payload is invalid. {payloadError.Message}");
        return false;
    }

    private static bool TryCreateOutput<TOutput> (
        PlayCommandExecutionContext context,
        ExecutionRef? lifecycleExecutionRef,
        PlayLifecycleTransitionResult transition,
        IPlayTransitionDirectionPolicy<TOutput> direction,
        out TOutput? output,
        out ApplicationFailure? failure)
        where TOutput : class
    {
        if (transition.Transition != direction.Transition)
        {
            output = null;
            failure = ApplicationFailure.InternalError(
                $"Unity {GetProtocolActionName(direction)} transition mismatch. Actual={transition.Transition}.");
            return false;
        }

        var currentSnapshot = transition.After ?? transition.Observed!;
        var validationFailure = ValidateTransitionSnapshots(
            context,
            transition,
            currentSnapshot,
            direction);
        if (validationFailure is not null)
        {
            output = null;
            failure = validationFailure;
            return false;
        }

        var lifecycle =
            PlayOutputProjectionFactory.CreateSnapshotOutput(currentSnapshot);
        if (lifecycle.EditorMode != UnityEditorMode.Gui)
        {
            output = null;
            failure = ApplicationFailure.InternalError(
                direction.RequiresGuiEditorMessage,
                PlayModeErrorCodes.PlayModeRequiresGuiEditor);
            return false;
        }

        if (lifecycleExecutionRef is not ITerminalExecutionRef
            terminalExecutionRef)
        {
            output = null;
            failure = ApplicationFailure.InternalError(
                $"Unity {GetProtocolActionName(direction)} response did not retain a terminal Lifecycle Execution reference.");
            return false;
        }
        if (terminalExecutionRef.State.Value
            != TextVocabulary.GetText(
                LifecycleExecutionState.Completed))
        {
            output = null;
            failure = ApplicationFailure.InternalError(
                $"Unity {GetProtocolActionName(direction)} success response did not retain a completed Lifecycle Execution reference.");
            return false;
        }

        output = direction.CreateOutput(
            context,
            terminalExecutionRef,
            lifecycle,
            PlayOutputProjectionFactory.CreateSuccessTransitionOutput(
                transition));
        failure = null;
        return true;
    }

    private static ApplicationFailure? ValidateTransitionSnapshots<TOutput> (
        PlayCommandExecutionContext context,
        PlayLifecycleTransitionResult transition,
        UnityEditorObservation currentSnapshot,
        IPlayTransitionDirectionPolicy<TOutput> direction)
        where TOutput : class
    {
        var beforeFailure = ValidateSnapshotProject(
            context,
            transition.Before,
            "before",
            direction);
        if (beforeFailure is not null)
        {
            return beforeFailure;
        }

        var currentFailure = ValidateSnapshotProject(
            context,
            currentSnapshot,
            "current",
            direction);
        return currentFailure
            ?? direction.ValidateTransitionSnapshots(
                transition,
                currentSnapshot);
    }

    private static ApplicationFailure? ValidateSnapshotProject<TOutput> (
        PlayCommandExecutionContext context,
        UnityEditorObservation snapshot,
        string label,
        IPlayTransitionDirectionPolicy<TOutput> direction)
        where TOutput : class
    {
        return snapshot.ProjectFingerprint
                == context.Project.ProjectFingerprint
            ? null
            : ApplicationFailure.InternalError(
                $"Unity {GetProtocolActionName(direction)} {label} projectFingerprint mismatch. Requested={context.Project.ProjectFingerprint}, Actual={snapshot.ProjectFingerprint}.");
    }

    private static ApplicationFailure CreateErrorFromUnityRequestFailure (
        UnityRequestFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Code switch
        {
            var code when code == ExecutionErrorCodes.Canceled =>
                ApplicationFailure.Canceled(failure.Message, failure.Code),
            var code when code == ExecutionErrorCodes.IpcTimeout =>
                ApplicationFailure.Timeout(failure.Message, failure.Code),
            _ => ApplicationFailure.InternalError(
                failure.Message,
                failure.Code),
        };
    }

    private static PlayTransitionFailureContext? CreateFailureContext (
        PlayCommandExecutionContext context,
        LifecycleExecutionStartBinding? start)
    {
        return CreateFailureContext(
            context,
            start?.LifecycleExecutionRef,
            ExecutionApplicationState.Indeterminate);
    }

    private static PlayTransitionFailureContext? CreateFailureContext (
        PlayCommandExecutionContext context,
        ExecutionRef? executionReference,
        ExecutionApplicationState applicationState)
    {
        return executionReference is null
            ? null
            : new PlayTransitionFailureContext(
                context.Project,
                executionReference,
                applicationState);
    }

    private static bool TryCreateTypedFailureContext<TOutput> (
        PlayCommandExecutionContext context,
        ExecutionRef executionReference,
        ExecutionApplicationState applicationState,
        PlayLifecycleTransitionResult transition,
        IPlayTransitionDirectionPolicy<TOutput> direction,
        out PlayTransitionFailureContext? failureContext,
        out ApplicationFailure? failure)
        where TOutput : class
    {
        if (transition.Transition != direction.Transition)
        {
            failureContext = null;
            failure = ApplicationFailure.InternalError(
                $"Unity {GetProtocolActionName(direction)} transition mismatch. Actual={transition.Transition}.");
            return false;
        }

        var currentSnapshot = transition.After ?? transition.Observed!;
        var validationFailure = ValidateTransitionSnapshots(
            context,
            transition,
            currentSnapshot,
            direction);
        if (validationFailure is not null)
        {
            failureContext = null;
            failure = validationFailure;
            return false;
        }

        var lifecycle =
            PlayOutputProjectionFactory.CreateSnapshotOutput(currentSnapshot);
        failureContext = new PlayTransitionFailureContext(
            context.Project,
            executionReference,
            applicationState,
            lifecycle,
            PlayOutputProjectionFactory.CreateTransitionOutput(transition),
            context.TimeoutMilliseconds);
        failure = null;
        return true;
    }

    private static ApplicationFailure CreateErrorFromResponse (
        UnityRequestResponse response)
    {
        var firstError = response.Errors[0];
        return firstError.Code == PlayModeErrorCodes.PlayModeTransitionTimeout
            ? ApplicationFailure.Timeout(
                firstError.Message,
                firstError.Code,
                firstError.InstancePath)
            : ApplicationFailure.FromCode(
                firstError.Code,
                firstError.Message,
                firstError.InstancePath);
    }

    private static bool TryCreateTypedResponseFailure<TOutput> (
        UnityRequestResponse response,
        ExecutionRef executionReference,
        PlayLifecycleTransitionResult transition,
        IPlayTransitionDirectionPolicy<TOutput> direction,
        out ApplicationFailure? failure)
        where TOutput : class
    {
        var actualCode = response.Errors[0].Code;
        var codeMatches = transition.IsSuccessful
            ? executionReference.Lifecycle switch
            {
                ExecutionLifecycle.Recovery =>
                    actualCode
                        == LifecycleExecutionErrorCodes
                            .TerminalPublicationFailed,
                ExecutionLifecycle.Terminal =>
                    actualCode
                        == LifecycleExecutionErrorCodes.DeadlineExceeded,
                _ => false,
            }
            : transition.Result switch
            {
                PlayLifecycleTransitionOutcome.Timeout =>
                    actualCode
                        == PlayModeErrorCodes.PlayModeTransitionTimeout,
                PlayLifecycleTransitionOutcome.Blocked =>
                    IsAllowedBlockedErrorCode(
                        actualCode,
                        direction.ActionRejectedCode),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(transition),
                    transition.Result,
                    "Unsupported Play Mode failure result."),
            };
        if (!codeMatches)
        {
            failure = ApplicationFailure.InternalError(
                $"Unity {GetProtocolActionName(direction)} error code '{actualCode}' does not match typed result '{transition.Result}'.");
            return false;
        }

        failure = CreateErrorFromResponse(response);
        return true;
    }

    private static bool IsAllowedBlockedErrorCode (
        UcliCode code,
        UcliCode actionRejectedCode)
    {
        return code == PlayModeErrorCodes.PlayModeRequiresGuiEditor
            || code == PlayModeErrorCodes.PlayModeStateUnknown
            || code == PlayModeErrorCodes.PlayModeAlreadyChanging
            || code == PlayModeErrorCodes.PlayModeTransitionBlocked
            || code == actionRejectedCode;
    }

    private static string GetProtocolActionName<TOutput> (
        IPlayTransitionDirectionPolicy<TOutput> direction)
        where TOutput : class
    {
        return direction.Command.Name.Replace('.', ' ');
    }
}
