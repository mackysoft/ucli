
namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported operation side-effect literals. </summary>
[VocabularyDefinition]
public enum UcliOperationSideEffect
{
    /// <summary> Observes live Unity state or read-index state. </summary>
    [VocabularyText("observesUnityState")]
    ObservesUnityState = 0,

    /// <summary> Changes Unity editor state. </summary>
    [VocabularyText("editorStateChange")]
    EditorStateChange = 1,

    /// <summary> Opens a scene in the Unity editor. </summary>
    [VocabularyText("opensSceneInEditor")]
    OpensSceneInEditor = 2,

    /// <summary> Opens a prefab stage in the Unity editor. </summary>
    [VocabularyText("opensPrefabStage")]
    OpensPrefabStage = 3,

    /// <summary> Refreshes Unity AssetDatabase state. </summary>
    [VocabularyText("assetDatabaseRefresh")]
    AssetDatabaseRefresh = 4,

    /// <summary> Imports Unity AssetDatabase assets. </summary>
    [VocabularyText("assetImport")]
    AssetImport = 5,

    /// <summary> Triggers script compilation. </summary>
    [VocabularyText("scriptCompilation")]
    ScriptCompilation = 6,

    /// <summary> Triggers a Unity domain reload. </summary>
    [VocabularyText("domainReload")]
    DomainReload = 7,

    /// <summary> Mutates scene content. </summary>
    [VocabularyText("sceneContentMutation")]
    SceneContentMutation = 8,

    /// <summary> Mutates prefab content. </summary>
    [VocabularyText("prefabContentMutation")]
    PrefabContentMutation = 9,

    /// <summary> Mutates asset content. </summary>
    [VocabularyText("assetContentMutation")]
    AssetContentMutation = 10,

    /// <summary> Mutates project settings. </summary>
    [VocabularyText("projectSettingsMutation")]
    ProjectSettingsMutation = 11,

    /// <summary> Saves scene files. </summary>
    [VocabularyText("sceneSave")]
    SceneSave = 12,

    /// <summary> Saves prefab files. </summary>
    [VocabularyText("prefabSave")]
    PrefabSave = 13,

    /// <summary> Saves asset files. </summary>
    [VocabularyText("assetSave")]
    AssetSave = 14,

    /// <summary> Saves project-wide state. </summary>
    [VocabularyText("projectSave")]
    ProjectSave = 15,

    /// <summary> Runs an external process or shell. </summary>
    [VocabularyText("externalProcess")]
    ExternalProcess = 16,

    /// <summary> Writes to the filesystem outside Unity Editor save boundaries. </summary>
    [VocabularyText("filesystemWrite")]
    FilesystemWrite = 17,

    /// <summary> Executes source code supplied by the caller. </summary>
    [VocabularyText("arbitrarySourceExecution")]
    ArbitrarySourceExecution = 18,

    /// <summary> Performs destructive operations whose target boundary is not sufficiently bounded. </summary>
    [VocabularyText("destructiveScope")]
    DestructiveScope = 19,

    /// <summary> Mutates Play Mode runtime state without persisting Unity project resources. </summary>
    [VocabularyText("runtimeStateMutation")]
    RuntimeStateMutation = 20,
}
