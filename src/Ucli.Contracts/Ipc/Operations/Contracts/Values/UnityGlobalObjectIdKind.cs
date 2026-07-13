namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the Unity object category encoded by a GlobalObjectId. </summary>
public enum UnityGlobalObjectIdKind
{
    /// <summary> No supported object category is specified. </summary>
    Unspecified = 0,

    /// <summary> An object imported from an asset. </summary>
    ImportedAsset = 1,

    /// <summary> An object persisted in a scene. </summary>
    SceneObject = 2,

    /// <summary> An object persisted in a source asset. </summary>
    SourceAsset = 3,
}
