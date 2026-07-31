using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a typed Play Mode Lifecycle Execution failure response. </summary>
public sealed record IpcPlayTransitionErrorResponse
{
    /// <summary> Initializes one failed Play Mode transition response. </summary>
    [JsonConstructor]
    public IpcPlayTransitionErrorResponse (
        ExecutionRef? lifecycleExecutionRef,
        ExecutionApplicationState applicationState,
        PlayLifecycleTransitionResult? result)
    {
        var expectedKind = result != null
            ? result.Transition == PlayLifecycleTransitionCommand.Enter
                ? LifecycleExecutionKind.PlayEnter
                : LifecycleExecutionKind.PlayExit
            : lifecycleExecutionRef != null
                ? LifecycleExecutionContractGuard.RequireReference(
                    lifecycleExecutionRef,
                    nameof(lifecycleExecutionRef))
                : LifecycleExecutionKind.PlayEnter;
        if (expectedKind is not LifecycleExecutionKind.PlayEnter
            and not LifecycleExecutionKind.PlayExit)
        {
            throw new ArgumentException(
                "Play Mode error response requires a play.enter or play.exit execution reference.",
                nameof(lifecycleExecutionRef));
        }
        LifecycleExecutionRef = IpcLifecycleExecutionContractGuard.RequireErrorReference(
            lifecycleExecutionRef,
            expectedKind,
            nameof(lifecycleExecutionRef));
        ApplicationState = IpcLifecycleExecutionContractGuard.RequireApplicationState(
            applicationState,
            nameof(applicationState));
        if (applicationState == ExecutionApplicationState.PartiallyApplied)
        {
            throw new ArgumentOutOfRangeException(
                nameof(applicationState),
                applicationState,
                "Play Mode transitions do not support a partially applied state.");
        }
        if (lifecycleExecutionRef == null)
        {
            if (applicationState != ExecutionApplicationState.NotApplied)
            {
                throw new ArgumentException(
                    "A Play Mode failure without a registered execution must be notApplied.",
                    nameof(applicationState));
            }
            if (result != null)
            {
                throw new ArgumentException(
                    "A Play Mode result cannot exist before execution registration.",
                    nameof(result));
            }
        }

        if (result?.IsSuccessful == true)
        {
            var retainsPublicationRecovery =
                lifecycleExecutionRef?.Lifecycle
                    == ExecutionLifecycle.Recovery
                && lifecycleExecutionRef.State.Value
                    == TextVocabulary.GetText(
                        LifecycleExecutionState.Publishing);
            var retainsPublishedTerminal =
                lifecycleExecutionRef?.Lifecycle
                    == ExecutionLifecycle.Terminal
                && lifecycleExecutionRef.State.Value
                    == TextVocabulary.GetText(
                        LifecycleExecutionState.Failed);
            if (!retainsPublicationRecovery
                && !retainsPublishedTerminal)
            {
                throw new ArgumentException(
                    "A successful Play Mode result in an error response requires either a publishing recovery reference or a failed terminal reference.",
                    nameof(lifecycleExecutionRef));
            }
        }

        if (result != null
            && result.OutcomeApplicationState != applicationState)
        {
            throw new ArgumentException(
                "Play Mode result and delivery envelope must report the same application state.",
                nameof(applicationState));
        }

        Result = result;
    }

    /// <summary> Gets the registered execution reference, or <see langword="null" /> before registration. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionRef? LifecycleExecutionRef { get; private init; }

    /// <summary> Gets whether the action was applied at the failed delivery boundary. </summary>
    [JsonInclude]
    [JsonRequired]
    public ExecutionApplicationState ApplicationState { get; private init; }

    /// <summary> Gets the typed action result when one was durably observed. </summary>
    [JsonInclude]
    [JsonRequired]
    public PlayLifecycleTransitionResult? Result { get; private init; }
}
