using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Readiness;

/// <summary> Applies daemon lifecycle readiness rules before dispatching readiness-gated requests. </summary>
internal static class UnityDaemonReadinessPolicy
{
    /// <summary> Determines whether a readiness-gated daemon response should be retried after a late waitable lifecycle regression. </summary>
    /// <param name="dispatchResult"> The daemon dispatch result. </param>
    /// <param name="failFast"> Whether fail-fast mode was requested by the original caller. </param>
    /// <returns> <see langword="true" /> when the caller may wait again and redispatch; otherwise <see langword="false" />. </returns>
    public static bool ShouldRetryAfterLateWaitableRegression (
        UnityRequestExecutionResult dispatchResult,
        bool failFast)
    {
        ArgumentNullException.ThrowIfNull(dispatchResult);

        if (failFast || !dispatchResult.IsSuccess)
        {
            return false;
        }

        var firstError = dispatchResult.Response!.Errors.FirstOrDefault();
        if (firstError == null)
        {
            return false;
        }

        return UnityEditorReadinessPolicy.IsWaitableRegressionError(firstError.Code);
    }
}
