using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

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
        public static UnityEditorLifecycleState Resolve (
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
                return UnityEditorLifecycleState.ShuttingDown;
            }

            if (isDomainReloading)
            {
                return UnityEditorLifecycleState.DomainReloading;
            }

            if (isCompiling)
            {
                return UnityEditorLifecycleState.Compiling;
            }

            if (hasCompileFailure)
            {
                return UnityEditorLifecycleState.CompileFailed;
            }

            if (isUpdating)
            {
                return UnityEditorLifecycleState.Reimporting;
            }

            if (isRecoveringPending)
            {
                return UnityEditorLifecycleState.Recovering;
            }

            if (isStartupPending)
            {
                return UnityEditorLifecycleState.Starting;
            }

            if (isPlaymodeActive)
            {
                return UnityEditorLifecycleState.PlayMode;
            }

            return UnityEditorLifecycleState.Ready;
        }
    }
}
