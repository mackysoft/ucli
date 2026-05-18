namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines side-effect literals exposed by operation assurance metadata. </summary>
public static class UcliOperationSideEffectValues
{
    /// <summary> Gets the value for observing live Unity state or read-index state. </summary>
    public const string ObservesUnityState = "observesUnityState";

    /// <summary> Gets the value for changing Unity editor state. </summary>
    public const string EditorStateChange = "editorStateChange";

    /// <summary> Gets the value for opening a scene in the Unity editor. </summary>
    public const string OpensSceneInEditor = "opensSceneInEditor";

    /// <summary> Gets the value for opening a prefab stage in the Unity editor. </summary>
    public const string OpensPrefabStage = "opensPrefabStage";

    /// <summary> Gets the value for refreshing Unity AssetDatabase state. </summary>
    public const string AssetDatabaseRefresh = "assetDatabaseRefresh";

    /// <summary> Gets the value for importing Unity AssetDatabase assets. </summary>
    public const string AssetImport = "assetImport";

    /// <summary> Gets the value for triggering script compilation. </summary>
    public const string ScriptCompilation = "scriptCompilation";

    /// <summary> Gets the value for triggering a Unity domain reload. </summary>
    public const string DomainReload = "domainReload";

    /// <summary> Gets the value for mutating scene content. </summary>
    public const string SceneContentMutation = "sceneContentMutation";

    /// <summary> Gets the value for mutating prefab content. </summary>
    public const string PrefabContentMutation = "prefabContentMutation";

    /// <summary> Gets the value for mutating asset content. </summary>
    public const string AssetContentMutation = "assetContentMutation";

    /// <summary> Gets the value for mutating project settings. </summary>
    public const string ProjectSettingsMutation = "projectSettingsMutation";

    /// <summary> Gets the value for saving scene files. </summary>
    public const string SceneSave = "sceneSave";

    /// <summary> Gets the value for saving prefab files. </summary>
    public const string PrefabSave = "prefabSave";

    /// <summary> Gets the value for writing asset files. </summary>
    public const string AssetSave = "assetSave";

    /// <summary> Gets the value for saving project-wide state. </summary>
    public const string ProjectSave = "projectSave";

    /// <summary> Gets the value for running an external process or shell. </summary>
    public const string ExternalProcess = "externalProcess";

    /// <summary> Gets the value for writing to the filesystem outside Unity Editor save boundaries. </summary>
    public const string FilesystemWrite = "filesystemWrite";

    /// <summary> Gets the value for executing source code supplied by the caller. </summary>
    public const string ArbitrarySourceExecution = "arbitrarySourceExecution";

    /// <summary> Gets the value for destructive operations whose target boundary is not sufficiently bounded. </summary>
    public const string DestructiveScope = "destructiveScope";
}
