using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary>
/// Defines an action-owned policy used by provider transports before they request a new
/// Lifecycle Execution Start Record.
/// </summary>
internal interface ILifecycleExecutionStartAdmissionPolicy
{
    /// <summary> Gets whether a waitable observation must fail immediately. </summary>
    bool FailFast { get; }

    /// <summary> Evaluates one provider observation before the first Start request. </summary>
    UnityReadinessDecision Evaluate (UnityEditorObservation observation);

    /// <summary>
    /// Determines whether a Start request that was definitely rejected before persistence may
    /// return to observation waiting and resend the same logical execution.
    /// </summary>
    bool ShouldRetryAfterRejectedStart (UcliCode errorCode);
}
