namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies concrete Unity-object reference representations. </summary>
[VocabularyDefinition]
public enum UcliReferenceKind
{
    /// <summary> References a value produced by an earlier request step. </summary>
    [VocabularyText("alias")]
    Alias = 1,

    /// <summary> References a Unity object by GlobalObjectId. </summary>
    [VocabularyText("globalObjectId")]
    GlobalObjectId = 2,

    /// <summary> References a Unity asset by GUID. </summary>
    [VocabularyText("assetGuid")]
    AssetGuid = 3,

    /// <summary> References a Unity asset by an Assets-relative path. </summary>
    [VocabularyText("assetPath")]
    AssetPath = 4,

    /// <summary> References a Unity project asset outside Assets. </summary>
    [VocabularyText("projectAssetPath")]
    ProjectAssetPath = 5,

    /// <summary> References a GameObject by scene and hierarchy path. </summary>
    [VocabularyText("sceneHierarchy")]
    SceneHierarchy = 6,

    /// <summary> References a GameObject by prefab and hierarchy path. </summary>
    [VocabularyText("prefabHierarchy")]
    PrefabHierarchy = 7,

    /// <summary> References a Component by scene, hierarchy path, and component type. </summary>
    [VocabularyText("sceneComponent")]
    SceneComponent = 8,

    /// <summary> References a Component by prefab, hierarchy path, and component type. </summary>
    [VocabularyText("prefabComponent")]
    PrefabComponent = 9,
}
