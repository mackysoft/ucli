using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Play.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary>
/// Projects typed Play Mode application results into the closed CLI error-payload union.
/// </summary>
internal static class PlayTransitionErrorCommandPayloadFactory
{
    public static JsonTypeInfo TypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(
            typeof(PlayTransitionErrorCommandPayload));

    public static IUcliNonNullJsonObject Empty ()
    {
        return Wrap(new EmptyPlayTransitionErrorCommandPayload());
    }

    public static IUcliNonNullJsonObject From (
        PlayEnterExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return From(
            result.IsSuccess,
            result.Error,
            result.FailureContext,
            LifecycleExecutionKind.PlayEnter,
            PlayLifecycleTransitionCommand.Enter);
    }

    public static IUcliNonNullJsonObject From (
        PlayExitExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return From(
            result.IsSuccess,
            result.Error,
            result.FailureContext,
            LifecycleExecutionKind.PlayExit,
            PlayLifecycleTransitionCommand.Exit);
    }

    private static IUcliNonNullJsonObject From (
        bool isSuccess,
        ApplicationFailure? error,
        PlayTransitionFailureContext? failureContext,
        LifecycleExecutionKind executionKind,
        PlayLifecycleTransitionCommand transition)
    {
        if (isSuccess || error == null)
        {
            throw new ArgumentException(
                "A Play Mode transition error payload requires a failed execution result.",
                nameof(error));
        }
        if (failureContext == null)
        {
            return Empty();
        }

        var lifecycleExecutionRef = RequireFailureReference(
            failureContext.LifecycleExecutionRef,
            executionKind);
        if (failureContext.CurrentLifecycle == null)
        {
            return Wrap(new PlayTransitionStartErrorCommandPayload(
                failureContext.Project,
                lifecycleExecutionRef,
                failureContext.ApplicationState));
        }

        var retainedTransition = failureContext.Transition
            ?? throw new ArgumentException(
                "Typed Play Mode transition evidence is required with a lifecycle snapshot.",
                nameof(failureContext));
        if (retainedTransition.Transition != transition)
        {
            throw new ArgumentException(
                "Retained Play Mode transition evidence does not match the command.",
                nameof(failureContext));
        }

        if (!PlayLifecycleTransitionResult.IsSuccessfulOutcome(
                retainedTransition.Result))
        {
            return Wrap(CreateTransitionFailure(
                failureContext,
                retainedTransition,
                lifecycleExecutionRef));
        }

        if (failureContext.LifecycleExecutionRef.Lifecycle
            == ExecutionLifecycle.Terminal)
        {
            if (error.Code
                != LifecycleExecutionErrorCodes.DeadlineExceeded)
            {
                throw new ArgumentException(
                    "A retained successful terminal result requires an error code that matches its non-completed Terminal Record.",
                    nameof(error));
            }

            return Wrap(CreateTerminalFailure(
                failureContext,
                retainedTransition,
                lifecycleExecutionRef));
        }

        if (error.Code
            != LifecycleExecutionErrorCodes.TerminalPublicationFailed)
        {
            throw new ArgumentException(
                "A retained successful recovery result requires a Terminal Record publication failure.",
                nameof(error));
        }

        return Wrap(CreateTerminalPublicationFailure(
            failureContext,
            retainedTransition,
            lifecycleExecutionRef));
    }

    private static PlayTransitionFailureErrorCommandPayload
        CreateTransitionFailure (
            PlayTransitionFailureContext failureContext,
            PlayTransitionOutput transition,
            ExecutionRef lifecycleExecutionRef)
    {
        var lifecycle = RequireLifecycle(failureContext);
        var timeoutMilliseconds = RequireTimeout(failureContext);
        if (transition.After != null
            || transition.Observed == null
            || !transition.ApplicationState.HasValue)
        {
            throw new ArgumentException(
                "Play Mode transition failure output requires before and observed snapshots without an after snapshot.",
                nameof(transition));
        }
        if (transition.ApplicationState.Value
            != failureContext.ApplicationState)
        {
            throw new ArgumentException(
                "Play Mode transition failure evidence and its command payload must report the same application state.",
                nameof(failureContext));
        }

        return new PlayTransitionFailureErrorCommandPayload(
            failureContext.Project,
            lifecycleExecutionRef,
            failureContext.ApplicationState,
            lifecycle,
            new PlayTransitionFailureCommandOutput(
                transition.Transition,
                transition.Result,
                transition.Before
                    ?? throw new ArgumentException(
                        "Play Mode transition failure output requires a before snapshot.",
                        nameof(transition)),
                transition.Observed,
                failureContext.ApplicationState),
            timeoutMilliseconds);
    }

    private static PlayTerminalFailureErrorCommandPayload
        CreateTerminalFailure (
            PlayTransitionFailureContext failureContext,
            PlayTransitionOutput transition,
            ExecutionRef lifecycleExecutionRef)
    {
        if (failureContext.LifecycleExecutionRef
                is not ITerminalExecutionRef terminalReference
            || lifecycleExecutionRef
                is not ITerminalExecutionRef)
        {
            throw new ArgumentException(
                "A terminal failure requires a finalized Lifecycle Execution reference.",
                nameof(failureContext));
        }

        return new PlayTerminalFailureErrorCommandPayload(
            failureContext.Project,
            terminalReference,
            failureContext.ApplicationState,
            RequireLifecycle(failureContext),
            CreateSuccessfulTransition(
                failureContext,
                transition),
            RequireTimeout(failureContext));
    }

    private static PlayTerminalPublicationFailureErrorCommandPayload
        CreateTerminalPublicationFailure (
            PlayTransitionFailureContext failureContext,
            PlayTransitionOutput transition,
            ExecutionRef lifecycleExecutionRef)
    {
        if (failureContext.LifecycleExecutionRef
                is not IRecoveryExecutionRef recoveryReference
            || lifecycleExecutionRef
                is not IRecoveryExecutionRef)
        {
            throw new ArgumentException(
                "A terminal-publication failure requires a recovery Lifecycle Execution reference.",
                nameof(failureContext));
        }

        return new PlayTerminalPublicationFailureErrorCommandPayload(
            failureContext.Project,
            recoveryReference,
            failureContext.ApplicationState,
            RequireLifecycle(failureContext),
            CreateSuccessfulTransition(
                failureContext,
                transition),
            RequireTimeout(failureContext));
    }

    private static ExecutionRef RequireFailureReference (
        ExecutionRef executionRef,
        LifecycleExecutionKind expectedKind)
    {
        LifecycleExecutionContractGuard.RequireFailureReference(
            executionRef,
            nameof(executionRef),
            expectedKind);
        return executionRef;
    }

    private static PlayTransitionSuccessOutput
        CreateSuccessfulTransition (
            PlayTransitionFailureContext failureContext,
            PlayTransitionOutput transition)
    {
        if (transition.After == null
            || transition.Observed != null
            || transition.ApplicationState.HasValue)
        {
            throw new ArgumentException(
                "A retained successful Play Mode result requires closed transition evidence.",
                nameof(failureContext));
        }

        var expectedApplicationState = transition.Result switch
        {
            PlayLifecycleTransitionOutcome.Entered
                or PlayLifecycleTransitionOutcome.Exited =>
                ExecutionApplicationState.Applied,
            PlayLifecycleTransitionOutcome.AlreadyEntered
                or PlayLifecycleTransitionOutcome.AlreadyExited =>
                ExecutionApplicationState.NotApplied,
            _ => throw new ArgumentOutOfRangeException(
                nameof(transition),
                transition.Result,
                "A retained successful result requires a successful Play Mode outcome."),
        };
        if (failureContext.ApplicationState != expectedApplicationState)
        {
            throw new ArgumentException(
                "A retained successful result must preserve the application state established by its outcome.",
                nameof(failureContext));
        }

        return new PlayTransitionSuccessOutput(
            transition.Transition,
            transition.Result,
            transition.Before,
            transition.After);
    }

    private static PlayLifecycleSnapshotOutput RequireLifecycle (
        PlayTransitionFailureContext failureContext)
    {
        return failureContext.CurrentLifecycle
            ?? throw new ArgumentException(
                "Typed Play Mode transition evidence requires a lifecycle snapshot.",
                nameof(failureContext));
    }

    private static int RequireTimeout (
        PlayTransitionFailureContext failureContext)
    {
        return failureContext.TimeoutMilliseconds
            ?? throw new ArgumentException(
                "Typed Play Mode transition evidence requires its execution deadline.",
                nameof(failureContext));
    }

    private static IUcliNonNullJsonObject Wrap (
        PlayTransitionErrorCommandPayload payload)
    {
        return UcliNonNullJsonObject.Wrap(
            payload,
            typeof(PlayTransitionErrorCommandPayload),
            CliOutputJsonSerializerOptions.Default);
    }
}
