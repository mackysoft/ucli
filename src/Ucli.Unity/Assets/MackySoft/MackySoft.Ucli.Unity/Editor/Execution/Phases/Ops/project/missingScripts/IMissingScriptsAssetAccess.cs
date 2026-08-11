using System.Collections.Generic;
using MackySoft.Ucli.Unity.SceneInspection;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Defines the Unity asset access used to discover and inspect saved missing-script targets. </summary>
    internal interface IMissingScriptsAssetAccess
    {
        MissingScriptsAssetAccessOutcome<IReadOnlyList<string>> FindAssets (string filter, string[] searchInFolders);

        MissingScriptsAssetAccessOutcome<string> GuidToAssetPath (string assetGuid);

        MissingScriptsAssetAccessOutcome<bool> IsSceneAsset (string assetPath);

        MissingScriptsAssetAccessOutcome<bool> IsPrefabAsset (string assetPath);

        MissingScriptsAssetAccessOutcome<SceneSourceLease> TryAcquirePersistedPreview (string assetPath);

        MissingScriptsAssetAccessOutcome<GameObject> LoadPrefabContents (string assetPath);

        void UnloadPrefabContents (GameObject prefabRoot);
    }
}
