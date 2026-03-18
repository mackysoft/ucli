using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Reports asset-save paths to the active project save capture scope. </summary>
    internal sealed class ProjectSaveAssetModificationProcessor : AssetModificationProcessor
    {
        /// <summary> Captures asset paths just before Unity persists them. </summary>
        /// <param name="paths"> The asset paths Unity is about to save. </param>
        /// <returns> The unchanged path array required by Unity callback contract. </returns>
        private static string[] OnWillSaveAssets (string[] paths)
        {
            return ProjectOperationCallbackRegistry.RecordSavePaths(paths);
        }
    }
}