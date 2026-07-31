using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;

/// <summary>
/// Owns the refresh action's caller-side observation and retry rules before a new durable Start
/// Record exists.
/// </summary>
internal sealed class RefreshLifecycleExecutionStartAdmissionPolicy :
    ILifecycleExecutionStartAdmissionPolicy
{
    /// <summary> Initializes one refresh start-admission policy. </summary>
    /// <param name="failFast"> Whether waitable refresh-entry states fail immediately. </param>
    public RefreshLifecycleExecutionStartAdmissionPolicy (bool failFast)
    {
        FailFast = failFast;
    }

    /// <inheritdoc />
    public bool FailFast { get; }

    /// <inheritdoc />
    public UnityReadinessDecision Evaluate (
        UnityEditorObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var decision = UnityEditorReadinessPolicy.Evaluate(
            observation,
            failFast: true);
        if (decision.IsReady
            || FailFast
            || !IsWaitableState(
                observation.State.LifecycleState))
        {
            return decision;
        }

        return UnityReadinessDecision.Wait();
    }

    /// <inheritdoc />
    public bool ShouldRetryAfterRejectedStart (UcliCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        return !FailFast
            && IsWaitableError(errorCode);
    }

    private static bool IsWaitableState (
        UnityEditorLifecycleState lifecycleState)
    {
        return lifecycleState is UnityEditorLifecycleState.Starting
            or UnityEditorLifecycleState.Busy
            or UnityEditorLifecycleState.Compiling;
    }

    private static bool IsWaitableError (UcliCode errorCode)
    {
        return errorCode == EditorLifecycleErrorCodes.EditorStarting
            || errorCode == EditorLifecycleErrorCodes.EditorBusy
            || errorCode == EditorLifecycleErrorCodes.EditorCompiling;
    }
}
