using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Resolves canonical editor lifecycle-state values from observed editor activity flags. </summary>
    internal static class UnityEditorLifecycleStateResolver
    {
        /// <summary> Resolves one lifecycle-state from observed editor activity flags. </summary>
        /// <param name="isStartupPending"> Whether startup is still in progress. </param>
        /// <param name="isShuttingDown"> Whether editor shutdown has started. </param>
        /// <param name="isPlaymodeActive"> Whether Play Mode is active or about to activate. </param>
        /// <param name="isDomainReloading"> Whether domain reload is in progress. </param>
        /// <param name="isCompiling"> Whether script compilation is in progress. </param>
        /// <param name="hasCompileFailure"> Whether the latest completed script compilation failed. </param>
        /// <param name="isUpdating"> Whether editor update/import work is in progress. </param>
        /// <param name="isRecoveringPending"> Whether daemon endpoint recovery is still in progress. </param>
        /// <returns> The lifecycle state. </returns>
        public static IpcEditorLifecycleState Resolve (
            bool isStartupPending,
            bool isShuttingDown,
            bool isPlaymodeActive,
            bool isDomainReloading,
            bool isCompiling,
            bool hasCompileFailure,
            bool isUpdating,
            bool isRecoveringPending)
        {
            if (isShuttingDown)
            {
                return IpcEditorLifecycleState.ShuttingDown;
            }

            if (isDomainReloading)
            {
                return IpcEditorLifecycleState.DomainReloading;
            }

            if (isCompiling)
            {
                return IpcEditorLifecycleState.Compiling;
            }

            if (hasCompileFailure)
            {
                return IpcEditorLifecycleState.CompileFailed;
            }

            if (isUpdating)
            {
                return IpcEditorLifecycleState.Reimporting;
            }

            if (isRecoveringPending)
            {
                return IpcEditorLifecycleState.Recovering;
            }

            if (isStartupPending)
            {
                return IpcEditorLifecycleState.Starting;
            }

            if (isPlaymodeActive)
            {
                return IpcEditorLifecycleState.PlayMode;
            }

            return IpcEditorLifecycleState.Ready;
        }
    }
}
