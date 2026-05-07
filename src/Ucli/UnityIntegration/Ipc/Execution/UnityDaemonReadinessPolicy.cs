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
        ArgumentNullException.ThrowIfNull(pingResponse);

        if (pingResponse.CanAcceptExecutionRequests)
        {
            return UnityDaemonReadinessDecision.Ready();
        }

        if (!IpcEditorLifecycleStateCodec.TryParse(pingResponse.LifecycleState, out var lifecycleState))
        {
            return UnityDaemonReadinessDecision.Failure(
                UcliCoreErrorCodes.InternalError,
                $"Unity editor lifecycle gate returned unsupported state '{pingResponse.LifecycleState}'.");
        }

        if (!failFast && IsWaitableLifecycleState(lifecycleState!))
        {
            return UnityDaemonReadinessDecision.Wait();
        }

        return lifecycleState switch
        {
            IpcEditorLifecycleStateCodec.Starting => UnityDaemonReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorStarting,
                "Unity editor startup is still in progress. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.Busy => UnityDaemonReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorBusy,
                "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.Compiling => UnityDaemonReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorCompiling,
                "Unity editor is compiling scripts. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.DomainReloading => UnityDaemonReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorDomainReloading,
                "Unity editor is reloading the AppDomain. Retry after lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.Playmode => UnityDaemonReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorPlaymode,
                "Unity editor is in Play Mode. Exit Play Mode and wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.BlockedByModal => UnityDaemonReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorModalBlocked,
                "Unity editor is blocked by a modal dialog. Resolve the dialog and wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.SafeMode => UnityDaemonReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorSafeMode,
                "Unity editor is in Safe Mode. Resolve compiler errors and wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.ShuttingDown => UnityDaemonReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorShuttingDown,
                "Unity editor is shutting down and cannot accept execution requests."),
            _ => UnityDaemonReadinessDecision.Failure(
                UcliCoreErrorCodes.InternalError,
                $"Unity editor lifecycle gate returned unsupported state '{lifecycleState}'."),
        };
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

        return firstError.Code == EditorLifecycleErrorCodes.EditorStarting
            || firstError.Code == EditorLifecycleErrorCodes.EditorBusy
            || firstError.Code == EditorLifecycleErrorCodes.EditorCompiling;
    }

    private static bool IsWaitableLifecycleState (string lifecycleState)
    {
        return string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Starting, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Busy, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Compiling, StringComparison.Ordinal);
    }
}
