using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Bridges Unity asset refresh callbacks into lifecycle generation telemetry. </summary>
    internal sealed class UnityAssetRefreshLifecycleObserver : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets (
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            UnityEditorReadinessGate.ObserveAssetRefreshCompleted();
        }
    }
}
