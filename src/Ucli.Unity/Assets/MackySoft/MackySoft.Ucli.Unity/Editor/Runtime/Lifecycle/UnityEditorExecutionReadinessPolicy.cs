using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

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
        public static bool IsWaitableState (IpcEditorLifecycleState lifecycleState)
        {
            return lifecycleState is IpcEditorLifecycleState.Starting
                or IpcEditorLifecycleState.Recovering
                or IpcEditorLifecycleState.Busy
                or IpcEditorLifecycleState.Compiling
                or IpcEditorLifecycleState.DomainReloading
                or IpcEditorLifecycleState.Reimporting;
        }

        /// <summary> Resolves the action required to clear one lifecycle state. </summary>
        public static DaemonDiagnosisActionRequired? ResolveActionRequired (IpcEditorLifecycleState lifecycleState)
        {
            return lifecycleState switch
            {
                IpcEditorLifecycleState.CompileFailed => DaemonDiagnosisActionRequired.FixCompileErrors,
                IpcEditorLifecycleState.ModalBlocked => DaemonDiagnosisActionRequired.ResolveUnityDialog,
                IpcEditorLifecycleState.SafeMode => DaemonDiagnosisActionRequired.ResolveUnityDialog,
                IpcEditorLifecycleState.Unavailable => DaemonDiagnosisActionRequired.InspectUnityLog,
                _ => null,
            };
        }

        /// <summary> Creates the blocked readiness result for one captured Unity Editor observation. </summary>
        /// <param name="observation"> The Unity Editor observation captured at decision time. </param>
        /// <returns> The blocked readiness result. </returns>
        public static UnityEditorExecutionReadinessResult CreateBlockedResult (UnityEditorObservation observation)
        {
            var error = observation.State.LifecycleState switch
            {
                IpcEditorLifecycleState.Starting => new IpcError(
                    EditorLifecycleErrorCodes.EditorStarting,
                    "Unity editor startup is still in progress. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleState.Recovering => new IpcError(
                    EditorLifecycleErrorCodes.EditorRecovering,
                    "Unity editor daemon endpoint is recovering. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleState.Busy => new IpcError(
                    EditorLifecycleErrorCodes.EditorBusy,
                    "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleState.Compiling => new IpcError(
                    EditorLifecycleErrorCodes.EditorCompiling,
                    "Unity editor is compiling scripts. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleState.CompileFailed => new IpcError(
                    EditorLifecycleErrorCodes.EditorCompileFailed,
                    "Unity editor has script compilation errors. Fix compiler errors and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleState.DomainReloading => new IpcError(
                    EditorLifecycleErrorCodes.EditorDomainReloading,
                    "Unity editor is reloading the AppDomain. Retry after lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleState.Reimporting => new IpcError(
                    EditorLifecycleErrorCodes.EditorReimporting,
                    "Unity editor is refreshing or reimporting assets. Retry without --failFast or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleState.PlayMode => new IpcError(
                    EditorLifecycleErrorCodes.EditorPlaymode,
                    "Unity editor is in Play Mode. Exit Play Mode and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleState.ModalBlocked => new IpcError(
                    EditorLifecycleErrorCodes.EditorModalBlocked,
                    "Unity editor is blocked by a modal dialog. Resolve the dialog and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleState.SafeMode => new IpcError(
                    EditorLifecycleErrorCodes.EditorSafeMode,
                    "Unity editor is in Safe Mode. Resolve compiler errors and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleState.ShuttingDown => new IpcError(
                    EditorLifecycleErrorCodes.EditorShuttingDown,
                    "Unity editor is shutting down and cannot accept execution requests.",
                    null),
                IpcEditorLifecycleState.Unavailable => new IpcError(
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
            UnityEditorObservation observation,
            bool isPlayModeActive)
        {
            if (observation.State.EditorMode != DaemonEditorMode.Gui)
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

            if (observation.State.LifecycleState != IpcEditorLifecycleState.PlayMode)
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
