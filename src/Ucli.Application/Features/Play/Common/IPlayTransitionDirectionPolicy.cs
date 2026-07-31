using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Play.Common;

/// <summary>
/// Owns the direction-specific contract used by the common Play Mode transition workflow.
/// </summary>
internal interface IPlayTransitionDirectionPolicy<TOutput>
    where TOutput : class
{
    UcliCommand Command { get; }

    LifecycleExecutionDefinition Definition { get; }

    PlayLifecycleTransitionCommand Transition { get; }

    UcliCode ActionRejectedCode { get; }

    string ActionDisplayName { get; }

    string CommandDisplayName { get; }

    string SessionNotAvailableMessage { get; }

    string RequiresGuiEditorMessage { get; }

    UnityRequestPayload CreatePayload (
        LifecycleExecutionRegistration registration,
        LifecycleExecutionStartBinding? requiredStart);

    LifecycleExecutionTerminalRecord CreateHostExitTerminalRecord (
        LifecycleExecutionStartBinding start,
        LifecycleExecutionTerminalFacts terminalFacts);

    bool TryGetTerminalResult (
        LifecycleExecutionTerminalRecord terminalRecord,
        out PlayLifecycleTransitionResult? result);

    ApplicationFailure? ValidateTransitionSnapshots (
        PlayLifecycleTransitionResult transition,
        UnityEditorObservation currentSnapshot);

    bool IsSuccessfulOutcome (PlayLifecycleTransitionOutcome outcome);

    TOutput CreateOutput (
        PlayCommandExecutionContext context,
        ITerminalExecutionRef terminalExecutionRef,
        PlayLifecycleSnapshotOutput lifecycle,
        PlayTransitionSuccessOutput transition);
}
