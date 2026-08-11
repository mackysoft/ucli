using System.Collections.Generic;
using MackySoft.Ucli.Unity.SceneInspection;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides production Unity Editor access for missing-script inspection. </summary>
    internal sealed class UnityMissingScriptsAssetAccess : IMissingScriptsAssetAccess
    {
        public MissingScriptsAssetAccessOutcome<IReadOnlyList<string>> FindAssets (string filter, string[] searchInFolders)
        {
            return MissingScriptsAssetAccessOutcome<IReadOnlyList<string>>.Available(
                AssetDatabase.FindAssets(filter, searchInFolders));
        }

        public MissingScriptsAssetAccessOutcome<string> GuidToAssetPath (string assetGuid)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            return string.IsNullOrEmpty(assetPath)
                ? MissingScriptsAssetAccessOutcome<string>.Unavailable()
                : MissingScriptsAssetAccessOutcome<string>.Available(assetPath);
        }

        public MissingScriptsAssetAccessOutcome<bool> IsSceneAsset (string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath) == null
                ? MissingScriptsAssetAccessOutcome<bool>.Unavailable()
                : MissingScriptsAssetAccessOutcome<bool>.Available(true);
        }

        public MissingScriptsAssetAccessOutcome<bool> IsPrefabAsset (string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null
                ? MissingScriptsAssetAccessOutcome<bool>.Unavailable()
                : MissingScriptsAssetAccessOutcome<bool>.Available(true);
        }

        public MissingScriptsAssetAccessOutcome<SceneSourceLease> TryAcquirePersistedPreview (string assetPath)
        {
            return SceneReadSourceResolver.TryAcquirePersistedPreview(assetPath, out var lease, out _)
                ? MissingScriptsAssetAccessOutcome<SceneSourceLease>.Available(lease)
                : MissingScriptsAssetAccessOutcome<SceneSourceLease>.Unavailable();
        }

        public MissingScriptsAssetAccessOutcome<GameObject> LoadPrefabContents (string assetPath)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
            return prefabRoot == null
                ? MissingScriptsAssetAccessOutcome<GameObject>.Unavailable()
                : MissingScriptsAssetAccessOutcome<GameObject>.Available(prefabRoot);
        }

        public void UnloadPrefabContents (GameObject prefabRoot)
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
