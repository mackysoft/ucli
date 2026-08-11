using MackySoft.Ucli.Unity.SceneInspection;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Defines the Unity asset access used to discover and inspect saved missing-script targets. </summary>
    internal interface IMissingScriptsAssetAccess
    {
        string[] FindAssets (string filter, string[] searchInFolders);

        string GuidToAssetPath (string assetGuid);

        bool IsSceneAsset (string assetPath);

        bool IsPrefabAsset (string assetPath);

        bool TryAcquirePersistedPreview (string assetPath, out SceneSourceLease lease);

        GameObject LoadPrefabContents (string assetPath);

        void UnloadPrefabContents (GameObject prefabRoot);
    }
}
