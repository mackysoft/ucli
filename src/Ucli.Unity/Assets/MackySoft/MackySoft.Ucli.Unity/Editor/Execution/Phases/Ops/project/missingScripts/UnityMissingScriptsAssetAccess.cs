using MackySoft.Ucli.Unity.SceneInspection;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides production Unity Editor access for missing-script inspection. </summary>
    internal sealed class UnityMissingScriptsAssetAccess : IMissingScriptsAssetAccess
    {
        public string[] FindAssets (string filter, string[] searchInFolders)
        {
            return AssetDatabase.FindAssets(filter, searchInFolders);
        }

        public string GuidToAssetPath (string assetGuid)
        {
            return AssetDatabase.GUIDToAssetPath(assetGuid);
        }

        public bool IsSceneAsset (string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath) != null;
        }

        public bool IsPrefabAsset (string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
        }

        public bool TryAcquirePersistedPreview (string assetPath, out SceneSourceLease lease)
        {
            return SceneReadSourceResolver.TryAcquirePersistedPreview(assetPath, out lease, out _);
        }

        public GameObject LoadPrefabContents (string assetPath)
        {
            return PrefabUtility.LoadPrefabContents(assetPath);
        }

        public void UnloadPrefabContents (GameObject prefabRoot)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
