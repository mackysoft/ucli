using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Maps lifecycle states to readiness-wait policy and blocked execution errors. </summary>
    internal static class UnityEditorExecutionReadinessPolicy
    {
        /// <summary> Determines whether the current lifecycle state may become ready by waiting. </summary>
        /// <param name="lifecycleState"> The canonical lifecycle-state literal. </param>
        /// <returns>
        /// <see langword="true" /> when the caller may continue waiting for readiness;
        /// otherwise, <see langword="false" />.
        /// </returns>
        public static bool IsWaitableState (string lifecycleState)
        {
            return string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Starting, System.StringComparison.Ordinal)
                || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Busy, System.StringComparison.Ordinal)
                || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Compiling, System.StringComparison.Ordinal)
                || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.DomainReloading, System.StringComparison.Ordinal);
        }

        /// <summary> Resolves the canonical blocking-reason literal for one lifecycle state. </summary>
        /// <param name="lifecycleState"> The canonical lifecycle-state literal. </param>
        /// <returns> The canonical blocking-reason literal, or <see langword="null" /> when execution is not blocked. </returns>
        public static string ResolveBlockingReason (string lifecycleState)
        {
            return lifecycleState switch
            {
                IpcEditorLifecycleStateCodec.Starting => IpcEditorBlockingReasonCodec.Startup,
                IpcEditorLifecycleStateCodec.Busy => IpcEditorBlockingReasonCodec.Busy,
                IpcEditorLifecycleStateCodec.Compiling => IpcEditorBlockingReasonCodec.Compile,
                IpcEditorLifecycleStateCodec.DomainReloading => IpcEditorBlockingReasonCodec.DomainReload,
                IpcEditorLifecycleStateCodec.Playmode => IpcEditorBlockingReasonCodec.PlayMode,
                IpcEditorLifecycleStateCodec.BlockedByModal => IpcEditorBlockingReasonCodec.ModalDialog,
                IpcEditorLifecycleStateCodec.SafeMode => IpcEditorBlockingReasonCodec.SafeMode,
                IpcEditorLifecycleStateCodec.ShuttingDown => IpcEditorBlockingReasonCodec.Shutdown,
                _ => null,
            };
        }

        /// <summary> Creates the blocked readiness result for one captured lifecycle snapshot. </summary>
        /// <param name="snapshot"> The lifecycle snapshot captured at decision time. </param>
        /// <returns> The blocked readiness result. </returns>
        public static UnityEditorExecutionReadinessResult CreateBlockedResult (UnityEditorLifecycleSnapshot snapshot)
        {
            var error = snapshot.LifecycleState switch
            {
                IpcEditorLifecycleStateCodec.Starting => new IpcError(
                    IpcErrorCodes.EditorStarting,
                    "Unity editor startup is still in progress. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.Busy => new IpcError(
                    IpcErrorCodes.EditorBusy,
                    "Unity editor is busy with internal work. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.Compiling => new IpcError(
                    IpcErrorCodes.EditorCompiling,
                    "Unity editor is compiling scripts. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.DomainReloading => new IpcError(
                    IpcErrorCodes.EditorDomainReloading,
                    "Unity editor is reloading the AppDomain. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.Playmode => new IpcError(
                    IpcErrorCodes.EditorPlaymode,
                    "Unity editor is in Play Mode. Exit Play Mode and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.BlockedByModal => new IpcError(
                    IpcErrorCodes.EditorModalBlocked,
                    "Unity editor is blocked by a modal dialog. Resolve the dialog and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.SafeMode => new IpcError(
                    IpcErrorCodes.EditorSafeMode,
                    "Unity editor is in Safe Mode. Resolve compiler errors and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.ShuttingDown => new IpcError(
                    IpcErrorCodes.EditorShuttingDown,
                    "Unity editor is shutting down and cannot accept execution requests.",
                    null),
                _ => new IpcError(
                    IpcErrorCodes.InternalError,
                    $"Unity editor lifecycle gate returned unsupported state '{snapshot.LifecycleState}'.",
                    null),
            };

            return UnityEditorExecutionReadinessResult.Blocked(snapshot, error);
        }
    }
}
