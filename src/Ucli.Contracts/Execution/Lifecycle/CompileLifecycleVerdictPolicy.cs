using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Evaluates the compile action's verdict from its provider-independent typed evidence.
/// </summary>
internal static class CompileLifecycleVerdictPolicy
{
    /// <summary>
    /// Returns the verdict established by the complete compile action result.
    /// </summary>
    internal static Verdict Evaluate (CompileLifecycleResult result)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var lifecycleState = result.Lifecycle.State;
        if (result.ScriptCompilation.Diagnostics.ErrorCount > 0
            || (lifecycleState is not null
                && !UnityEditorLifecycleSemantics.CanAcceptExecutionRequests(
                    lifecycleState.LifecycleState)))
        {
            return Verdict.Fail;
        }

        if (!result.Refresh.Completed
            || !result.ScriptCompilation.Completed
            || !result.DomainReload.Settled
            || lifecycleState is null)
        {
            return Verdict.Incomplete;
        }

        return Verdict.Pass;
    }
}
