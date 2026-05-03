namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines side-effect literals exposed by operation assurance metadata. </summary>
public static class UcliOperationSideEffectValues
{
    /// <summary> Gets the value for opening a scene in the Unity editor. </summary>
    public const string OpensSceneInEditor = "opensSceneInEditor";

    /// <summary> Gets the value for opening a prefab stage in the Unity editor. </summary>
    public const string OpensPrefabStage = "opensPrefabStage";

    /// <summary> Gets the value for refreshing Unity AssetDatabase state. </summary>
    public const string RefreshesAssetDatabase = "refreshesAssetDatabase";

    /// <summary> Gets the value for writing asset files. </summary>
    public const string WritesAsset = "writesAsset";

    /// <summary> Gets the value for writing scene files. </summary>
    public const string WritesScene = "writesScene";

    /// <summary> Gets the value for writing prefab files. </summary>
    public const string WritesPrefab = "writesPrefab";

    /// <summary> Gets the value for writing project settings files. </summary>
    public const string WritesProjectSettings = "writesProjectSettings";
}
