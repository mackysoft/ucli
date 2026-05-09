using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Unity.Runtime
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
                || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Recovering, System.StringComparison.Ordinal)
                || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Busy, System.StringComparison.Ordinal)
                || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Compiling, System.StringComparison.Ordinal)
                || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.DomainReloading, System.StringComparison.Ordinal)
                || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Reimporting, System.StringComparison.Ordinal);
        }

        /// <summary> Resolves the canonical blocking-reason literal for one lifecycle state. </summary>
        /// <param name="lifecycleState"> The canonical lifecycle-state literal. </param>
        /// <returns> The canonical blocking-reason literal, or <see langword="null" /> when execution is not blocked. </returns>
        public static string ResolveBlockingReason (string lifecycleState)
        {
            return lifecycleState switch
            {
                IpcEditorLifecycleStateCodec.Starting => IpcEditorBlockingReasonCodec.Startup,
                IpcEditorLifecycleStateCodec.Recovering => IpcEditorBlockingReasonCodec.Recovery,
                IpcEditorLifecycleStateCodec.Busy => IpcEditorBlockingReasonCodec.Busy,
                IpcEditorLifecycleStateCodec.Compiling => IpcEditorBlockingReasonCodec.Compile,
                IpcEditorLifecycleStateCodec.CompileFailed => IpcEditorBlockingReasonCodec.CompileFailed,
                IpcEditorLifecycleStateCodec.DomainReloading => IpcEditorBlockingReasonCodec.DomainReload,
                IpcEditorLifecycleStateCodec.Reimporting => IpcEditorBlockingReasonCodec.Reimport,
                IpcEditorLifecycleStateCodec.Playmode => IpcEditorBlockingReasonCodec.PlayMode,
                IpcEditorLifecycleStateCodec.ModalBlocked => IpcEditorBlockingReasonCodec.ModalDialog,
                IpcEditorLifecycleStateCodec.SafeMode => IpcEditorBlockingReasonCodec.SafeMode,
                IpcEditorLifecycleStateCodec.ShuttingDown => IpcEditorBlockingReasonCodec.Shutdown,
                IpcEditorLifecycleStateCodec.Unavailable => IpcEditorBlockingReasonCodec.Unavailable,
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
                    EditorLifecycleErrorCodes.EditorStarting,
                    "Unity editor startup is still in progress. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.Recovering => new IpcError(
                    EditorLifecycleErrorCodes.EditorRecovering,
                    "Unity editor daemon endpoint is recovering. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.Busy => new IpcError(
                    EditorLifecycleErrorCodes.EditorBusy,
                    "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.Compiling => new IpcError(
                    EditorLifecycleErrorCodes.EditorCompiling,
                    "Unity editor is compiling scripts. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.CompileFailed => new IpcError(
                    EditorLifecycleErrorCodes.EditorCompileFailed,
                    "Unity editor has script compilation errors. Fix compiler errors and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.DomainReloading => new IpcError(
                    EditorLifecycleErrorCodes.EditorDomainReloading,
                    "Unity editor is reloading the AppDomain. Retry after lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.Reimporting => new IpcError(
                    EditorLifecycleErrorCodes.EditorReimporting,
                    "Unity editor is refreshing or reimporting assets. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.Playmode => new IpcError(
                    EditorLifecycleErrorCodes.EditorPlaymode,
                    "Unity editor is in Play Mode. Exit Play Mode and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.ModalBlocked => new IpcError(
                    EditorLifecycleErrorCodes.EditorModalBlocked,
                    "Unity editor is blocked by a modal dialog. Resolve the dialog and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.SafeMode => new IpcError(
                    EditorLifecycleErrorCodes.EditorSafeMode,
                    "Unity editor is in Safe Mode. Resolve compiler errors and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.ShuttingDown => new IpcError(
                    EditorLifecycleErrorCodes.EditorShuttingDown,
                    "Unity editor is shutting down and cannot accept execution requests.",
                    null),
                IpcEditorLifecycleStateCodec.Unavailable => new IpcError(
                    EditorLifecycleErrorCodes.EditorUnavailable,
                    "Unity editor lifecycle is unavailable because the daemon endpoint cannot be observed.",
                    null),
                _ => new IpcError(
                    UcliCoreErrorCodes.InternalError,
                    $"Unity editor lifecycle gate returned unsupported state '{snapshot.LifecycleState}'.",
                    null),
            };

            return UnityEditorExecutionReadinessResult.Blocked(snapshot, error);
        }
    }
}
