namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines supported operation side-effect literals. </summary>
public enum UcliOperationSideEffect
{
    /// <summary> Observes live Unity state or read-index state. </summary>
    ObservesUnityState = 0,

    /// <summary> Changes Unity editor state. </summary>
    EditorStateChange = 1,

    /// <summary> Opens a scene in the Unity editor. </summary>
    OpensSceneInEditor = 2,

    /// <summary> Opens a prefab stage in the Unity editor. </summary>
    OpensPrefabStage = 3,

    /// <summary> Refreshes Unity AssetDatabase state. </summary>
    AssetDatabaseRefresh = 4,

    /// <summary> Imports Unity AssetDatabase assets. </summary>
    AssetImport = 5,

    /// <summary> Triggers script compilation. </summary>
    ScriptCompilation = 6,

    /// <summary> Triggers a Unity domain reload. </summary>
    DomainReload = 7,

    /// <summary> Mutates scene content. </summary>
    SceneContentMutation = 8,

    /// <summary> Mutates prefab content. </summary>
    PrefabContentMutation = 9,

    /// <summary> Mutates asset content. </summary>
    AssetContentMutation = 10,

    /// <summary> Mutates project settings. </summary>
    ProjectSettingsMutation = 11,

    /// <summary> Saves scene files. </summary>
    SceneSave = 12,

    /// <summary> Saves prefab files. </summary>
    PrefabSave = 13,

    /// <summary> Saves asset files. </summary>
    AssetSave = 14,

    /// <summary> Saves project-wide state. </summary>
    ProjectSave = 15,

    /// <summary> Runs an external process or shell. </summary>
    ExternalProcess = 16,

    /// <summary> Writes to the filesystem outside Unity Editor save boundaries. </summary>
    FilesystemWrite = 17,

    /// <summary> Executes source code supplied by the caller. </summary>
    ArbitrarySourceExecution = 18,

    /// <summary> Performs destructive operations whose target boundary is not sufficiently bounded. </summary>
    DestructiveScope = 19,
}
