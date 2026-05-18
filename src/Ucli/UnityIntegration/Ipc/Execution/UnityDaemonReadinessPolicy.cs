using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Applies daemon lifecycle readiness rules before dispatching readiness-gated requests. </summary>
internal static class UnityDaemonReadinessPolicy
{
    /// <summary> Evaluates one daemon ping payload against readiness requirements. </summary>
    /// <param name="pingResponse"> The decoded daemon ping payload. </param>
    /// <param name="failFast"> Whether waitable states should fail immediately. </param>
    /// <returns> The readiness decision. </returns>
    public static UnityDaemonReadinessDecision Evaluate (
        IpcPingResponse pingResponse,
        bool failFast)
    {
        var decision = UnityEditorReadinessPolicy.Evaluate(pingResponse, failFast);
        if (decision.IsReady)
        {
            return UnityDaemonReadinessDecision.Ready();
        }

        if (decision.IsFailure)
        {
            return UnityDaemonReadinessDecision.Failure(
                decision.ErrorCode!.Value,
                decision.ErrorMessage!);
        }

        return UnityDaemonReadinessDecision.Wait();
    }

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
