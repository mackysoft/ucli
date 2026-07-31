using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Maps lifecycle states to readiness-wait policy and blocked execution errors. </summary>
    internal static class UnityEditorExecutionReadinessPolicy
    {
        /// <summary> Determines whether the current lifecycle state may become ready by waiting. </summary>
        /// <param name="lifecycleState"> The lifecycle state. </param>
        /// <returns>
        /// <see langword="true" /> when the caller may continue waiting for readiness;
        /// otherwise, <see langword="false" />.
        /// </returns>
        public static bool IsWaitableState (UnityEditorLifecycleState lifecycleState)
        {
            return lifecycleState is UnityEditorLifecycleState.Starting
                or UnityEditorLifecycleState.Recovering
                or UnityEditorLifecycleState.Busy
                or UnityEditorLifecycleState.Compiling
                or UnityEditorLifecycleState.DomainReloading
                or UnityEditorLifecycleState.Reimporting;
        }

        /// <summary> Resolves the action required to clear one lifecycle state. </summary>
        public static UnityEditorActionRequired? ResolveActionRequired (UnityEditorLifecycleState lifecycleState)
        {
            return lifecycleState switch
            {
                UnityEditorLifecycleState.CompileFailed => UnityEditorActionRequired.FixCompileErrors,
                UnityEditorLifecycleState.ModalBlocked => UnityEditorActionRequired.ResolveUnityDialog,
                UnityEditorLifecycleState.SafeMode => UnityEditorActionRequired.ResolveUnityDialog,
                UnityEditorLifecycleState.Unavailable => UnityEditorActionRequired.InspectUnityLog,
                _ => null,
            };
        }

        /// <summary> Creates the blocked readiness result for one captured Unity Editor observation. </summary>
        /// <param name="observation"> The Unity Editor observation captured at decision time. </param>
        /// <returns> The blocked readiness result. </returns>
        public static UnityEditorExecutionReadinessResult CreateBlockedResult (UnityEditorRuntimeObservation observation)
        {
            var error = observation.State.LifecycleState switch
            {
                UnityEditorLifecycleState.Starting => new IpcError(
                    EditorLifecycleErrorCodes.EditorStarting,
                    "Unity editor startup is still in progress. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                UnityEditorLifecycleState.Recovering => new IpcError(
                    EditorLifecycleErrorCodes.EditorRecovering,
                    "Unity editor daemon endpoint is recovering. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                UnityEditorLifecycleState.Busy => new IpcError(
                    EditorLifecycleErrorCodes.EditorBusy,
                    "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                UnityEditorLifecycleState.Compiling => new IpcError(
                    EditorLifecycleErrorCodes.EditorCompiling,
                    "Unity editor is compiling scripts. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                UnityEditorLifecycleState.CompileFailed => new IpcError(
                    EditorLifecycleErrorCodes.EditorCompileFailed,
                    "Unity editor has script compilation errors. Fix compiler errors and wait until lifecycleState=ready before executing request.",
                    null),
                UnityEditorLifecycleState.DomainReloading => new IpcError(
                    EditorLifecycleErrorCodes.EditorDomainReloading,
                    "Unity editor is reloading the AppDomain. Retry after lifecycleState=ready before executing request.",
                    null),
                UnityEditorLifecycleState.Reimporting => new IpcError(
                    EditorLifecycleErrorCodes.EditorReimporting,
                    "Unity editor is refreshing or reimporting assets. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                UnityEditorLifecycleState.PlayMode => new IpcError(
                    EditorLifecycleErrorCodes.EditorPlaymode,
                    "Unity editor is in Play Mode. Exit Play Mode and wait until lifecycleState=ready before executing request.",
                    null),
                UnityEditorLifecycleState.ModalBlocked => new IpcError(
                    EditorLifecycleErrorCodes.EditorModalBlocked,
                    "Unity editor is blocked by a modal dialog. Resolve the dialog and wait until lifecycleState=ready before executing request.",
                    null),
                UnityEditorLifecycleState.SafeMode => new IpcError(
                    EditorLifecycleErrorCodes.EditorSafeMode,
                    "Unity editor is in Safe Mode. Resolve compiler errors and wait until lifecycleState=ready before executing request.",
                    null),
                UnityEditorLifecycleState.ShuttingDown => new IpcError(
                    EditorLifecycleErrorCodes.EditorShuttingDown,
                    "Unity editor is shutting down and cannot accept execution requests.",
                    null),
                UnityEditorLifecycleState.Unavailable => new IpcError(
                    EditorLifecycleErrorCodes.EditorUnavailable,
                    "Unity editor lifecycle is unavailable because the daemon endpoint cannot be observed.",
                    null),
                _ => new IpcError(
                    UcliCoreErrorCodes.InternalError,
                    $"Unity editor lifecycle gate returned unsupported state value '{(int)observation.State.LifecycleState}'.",
                    null),
            };

            return UnityEditorExecutionReadinessResult.Blocked(observation, error);
        }

        /// <summary> Creates the readiness result for a request that explicitly allows Play Mode mutation. </summary>
        /// <param name="observation"> The Unity Editor observation captured at decision time. </param>
        /// <param name="isPlayModeActive"> Whether Unity reports active Play Mode, excluding enter/exit transitions. </param>
        /// <returns> A ready result when GUI Play Mode is active; otherwise a Play Mode contract error. </returns>
        public static UnityEditorExecutionReadinessResult CreatePlayModeAllowedResult (
            UnityEditorRuntimeObservation observation,
            bool isPlayModeActive)
        {
            if (observation.State.EditorMode != UnityEditorMode.Gui)
            {
                return UnityEditorExecutionReadinessResult.Blocked(
                    observation,
                    new IpcError(
                        PlayModeErrorCodes.PlayModeRequiresGuiEditor,
                        "Play Mode mutation requires a GUI Editor session.",
                        null));
            }

            if (!isPlayModeActive)
            {
                return UnityEditorExecutionReadinessResult.Blocked(
                    observation,
                    new IpcError(
                        PlayModeErrorCodes.PlayModeNotActive,
                        "Play Mode mutation requires the target Unity Editor to be in Play Mode.",
                        null));
            }

            if (observation.State.LifecycleState != UnityEditorLifecycleState.PlayMode)
            {
                return observation.CanAcceptExecutionRequests
                    ? UnityEditorExecutionReadinessResult.Blocked(
                        observation,
                        new IpcError(
                            PlayModeErrorCodes.PlayModeNotActive,
                            "Play Mode mutation requires the target Unity Editor to be in Play Mode.",
                            null))
                    : CreateBlockedResult(observation);
            }

            return UnityEditorExecutionReadinessResult.Ready(observation);
        }
    }
}
