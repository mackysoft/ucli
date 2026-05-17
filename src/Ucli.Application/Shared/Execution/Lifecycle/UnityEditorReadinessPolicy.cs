using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary> Applies Unity editor lifecycle readiness rules before execution-gated work. </summary>
internal static class UnityEditorReadinessPolicy
{
    /// <summary> Evaluates one ping payload against readiness requirements. </summary>
    /// <param name="pingResponse"> The decoded ping payload. </param>
    /// <param name="failFast"> Whether waitable states should fail immediately. </param>
    /// <returns> The readiness decision. </returns>
    public static UnityReadinessDecision Evaluate (
        IpcPingResponse pingResponse,
        bool failFast)
    {
        ArgumentNullException.ThrowIfNull(pingResponse);

        if (!IpcEditorLifecycleStateCodec.TryParse(pingResponse.LifecycleState, out var lifecycleState))
        {
            return UnityReadinessDecision.Failure(
                UcliCoreErrorCodes.InternalError,
                $"Unity editor lifecycle gate returned unsupported state '{pingResponse.LifecycleState}'.");
        }

        if (pingResponse.CanAcceptExecutionRequests
            && string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal))
        {
            return UnityReadinessDecision.Ready();
        }

        if (!failFast && IsWaitableLifecycleState(lifecycleState!))
        {
            return UnityReadinessDecision.Wait();
        }

        return lifecycleState switch
        {
            IpcEditorLifecycleStateCodec.Starting => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorStarting,
                "Unity editor startup is still in progress. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.Recovering => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorRecovering,
                "Unity editor daemon endpoint is recovering. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.Busy => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorBusy,
                "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.Compiling => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorCompiling,
                "Unity editor is compiling scripts. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.CompileFailed => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorCompileFailed,
                "Unity editor has script compilation errors. Fix compiler errors and wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.DomainReloading => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorDomainReloading,
                "Unity editor is reloading the AppDomain. Retry after lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.Reimporting => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorReimporting,
                "Unity editor is refreshing or reimporting assets. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.Playmode => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorPlaymode,
                "Unity editor is in Play Mode. Exit Play Mode and wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.ModalBlocked => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorModalBlocked,
                "Unity editor is blocked by a modal dialog. Resolve the dialog and wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.SafeMode => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorSafeMode,
                "Unity editor is in Safe Mode. Resolve compiler errors and wait until lifecycleState=ready before executing request."),
            IpcEditorLifecycleStateCodec.ShuttingDown => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorShuttingDown,
                "Unity editor is shutting down and cannot accept execution requests."),
            IpcEditorLifecycleStateCodec.Unavailable => UnityReadinessDecision.Failure(
                EditorLifecycleErrorCodes.EditorUnavailable,
                "Unity editor lifecycle is unavailable because the daemon endpoint cannot be observed."),
            _ => UnityReadinessDecision.Failure(
                UcliCoreErrorCodes.InternalError,
                $"Unity editor lifecycle gate returned unsupported state '{lifecycleState}'."),
        };
    }

    /// <summary> Determines whether an error code represents a waitable late lifecycle regression. </summary>
    public static bool IsWaitableRegressionError (UcliCodeValue errorCode)
    {
        return errorCode == EditorLifecycleErrorCodes.EditorStarting
            || errorCode == EditorLifecycleErrorCodes.EditorBusy
            || errorCode == EditorLifecycleErrorCodes.EditorCompiling
            || errorCode == EditorLifecycleErrorCodes.EditorRecovering
            || errorCode == EditorLifecycleErrorCodes.EditorReimporting;
    }

    /// <summary> Determines whether an error code was produced by Unity editor lifecycle readiness evaluation. </summary>
    public static bool IsReadinessFailureCode (UcliCodeValue errorCode)
    {
        return errorCode == EditorLifecycleErrorCodes.EditorStarting
            || errorCode == EditorLifecycleErrorCodes.EditorBusy
            || errorCode == EditorLifecycleErrorCodes.EditorCompiling
            || errorCode == EditorLifecycleErrorCodes.EditorCompileFailed
            || errorCode == EditorLifecycleErrorCodes.EditorDomainReloading
            || errorCode == EditorLifecycleErrorCodes.EditorRecovering
            || errorCode == EditorLifecycleErrorCodes.EditorReimporting
            || errorCode == EditorLifecycleErrorCodes.EditorPlaymode
            || errorCode == EditorLifecycleErrorCodes.EditorModalBlocked
            || errorCode == EditorLifecycleErrorCodes.EditorSafeMode
            || errorCode == EditorLifecycleErrorCodes.EditorShuttingDown
            || errorCode == EditorLifecycleErrorCodes.EditorUnavailable;
    }

    private static bool IsWaitableLifecycleState (string lifecycleState)
    {
        return string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Starting, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Recovering, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Busy, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Compiling, StringComparison.Ordinal)
            || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Reimporting, StringComparison.Ordinal);
    }
}
