using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
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
        /// <param name="isUpdating"> Whether editor update/import work is in progress. </param>
        /// <returns> The canonical lifecycle-state literal. </returns>
        public static string Resolve (
            bool isStartupPending,
            bool isShuttingDown,
            bool isPlaymodeActive,
            bool isDomainReloading,
            bool isCompiling,
            bool isUpdating)
        {
            if (isShuttingDown)
            {
                return IpcEditorLifecycleStateCodec.ShuttingDown;
            }

            if (isPlaymodeActive)
            {
                return IpcEditorLifecycleStateCodec.Playmode;
            }

            if (isDomainReloading)
            {
                return IpcEditorLifecycleStateCodec.DomainReloading;
            }

            if (isCompiling)
            {
                return IpcEditorLifecycleStateCodec.Compiling;
            }

            if (isUpdating)
            {
                return IpcEditorLifecycleStateCodec.Busy;
            }

            if (isStartupPending)
            {
                return IpcEditorLifecycleStateCodec.Starting;
            }

            return IpcEditorLifecycleStateCodec.Ready;
        }
    }
}
