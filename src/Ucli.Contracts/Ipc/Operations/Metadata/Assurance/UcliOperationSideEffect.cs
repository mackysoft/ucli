namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines supported operation side-effect literals. </summary>
public enum UcliOperationSideEffect
{
    /// <summary> Opens a scene in the Unity editor. </summary>
    OpensSceneInEditor = 0,

    /// <summary> Opens a prefab stage in the Unity editor. </summary>
    OpensPrefabStage = 1,

    /// <summary> Refreshes Unity AssetDatabase state. </summary>
    RefreshesAssetDatabase = 2,

    /// <summary> Writes asset files. </summary>
    WritesAsset = 3,

    /// <summary> Writes scene files. </summary>
    WritesScene = 4,

    /// <summary> Writes prefab files. </summary>
    WritesPrefab = 5,

    /// <summary> Writes project settings files. </summary>
    WritesProjectSettings = 6,
}
