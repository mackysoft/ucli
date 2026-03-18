using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Reports asset refresh changes to the active project refresh capture scope. </summary>
    internal sealed class ProjectRefreshAssetPostprocessor : AssetPostprocessor
    {
        /// <summary> Captures imported, deleted, and moved asset paths after one refresh cycle. </summary>
        /// <param name="importedAssets"> The imported asset paths. </param>
        /// <param name="deletedAssets"> The deleted asset paths. </param>
        /// <param name="movedAssets"> The moved-to asset paths. </param>
        /// <param name="movedFromAssetPaths"> The moved-from asset paths. </param>
        private static void OnPostprocessAllAssets (
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            ProjectOperationCallbackRegistry.RecordRefreshPaths(
                importedAssets,
                deletedAssets,
                movedAssets,
                movedFromAssetPaths);
        }
    }
}