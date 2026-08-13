using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary> Applies the common wait-or-refuse admission rule before a Lifecycle action persists its Start record. </summary>
internal sealed class WaitableEditorLifecycleStartAdmissionPolicy : ILifecycleExecutionStartAdmissionPolicy
{
    public WaitableEditorLifecycleStartAdmissionPolicy (bool failFast) => FailFast = failFast;

    public bool FailFast { get; }

    public UnityReadinessDecision Evaluate (UnityEditorObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var decision = UnityEditorReadinessPolicy.Evaluate(observation, failFast: true);
        return decision.IsReady || FailFast || !IsWaitable(observation.State.LifecycleState)
            ? decision
            : UnityReadinessDecision.Wait();
    }

    public bool ShouldRetryAfterRejectedStart (UcliCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        return !FailFast && (errorCode == EditorLifecycleErrorCodes.EditorStarting
            || errorCode == EditorLifecycleErrorCodes.EditorBusy
            || errorCode == EditorLifecycleErrorCodes.EditorCompiling);
    }

    private static bool IsWaitable (UnityEditorLifecycleState state) => state is UnityEditorLifecycleState.Starting
        or UnityEditorLifecycleState.Busy or UnityEditorLifecycleState.Compiling;
}
