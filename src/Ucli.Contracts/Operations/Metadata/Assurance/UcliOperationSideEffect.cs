using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported operation side-effect literals. </summary>
public enum UcliOperationSideEffect
{
    /// <summary> Observes live Unity state or read-index state. </summary>
    [UcliContractLiteral("observesUnityState")]
    ObservesUnityState = 0,

    /// <summary> Changes Unity editor state. </summary>
    [UcliContractLiteral("editorStateChange")]
    EditorStateChange = 1,

    /// <summary> Opens a scene in the Unity editor. </summary>
    [UcliContractLiteral("opensSceneInEditor")]
    OpensSceneInEditor = 2,

    /// <summary> Opens a prefab stage in the Unity editor. </summary>
    [UcliContractLiteral("opensPrefabStage")]
    OpensPrefabStage = 3,

    /// <summary> Refreshes Unity AssetDatabase state. </summary>
    [UcliContractLiteral("assetDatabaseRefresh")]
    AssetDatabaseRefresh = 4,

    /// <summary> Imports Unity AssetDatabase assets. </summary>
    [UcliContractLiteral("assetImport")]
    AssetImport = 5,

    /// <summary> Triggers script compilation. </summary>
    [UcliContractLiteral("scriptCompilation")]
    ScriptCompilation = 6,

    /// <summary> Triggers a Unity domain reload. </summary>
    [UcliContractLiteral("domainReload")]
    DomainReload = 7,

    /// <summary> Mutates scene content. </summary>
    [UcliContractLiteral("sceneContentMutation")]
    SceneContentMutation = 8,

    /// <summary> Mutates prefab content. </summary>
    [UcliContractLiteral("prefabContentMutation")]
    PrefabContentMutation = 9,

    /// <summary> Mutates asset content. </summary>
    [UcliContractLiteral("assetContentMutation")]
    AssetContentMutation = 10,

    /// <summary> Mutates project settings. </summary>
    [UcliContractLiteral("projectSettingsMutation")]
    ProjectSettingsMutation = 11,

    /// <summary> Saves scene files. </summary>
    [UcliContractLiteral("sceneSave")]
    SceneSave = 12,

    /// <summary> Saves prefab files. </summary>
    [UcliContractLiteral("prefabSave")]
    PrefabSave = 13,

    /// <summary> Saves asset files. </summary>
    [UcliContractLiteral("assetSave")]
    AssetSave = 14,

    /// <summary> Saves project-wide state. </summary>
    [UcliContractLiteral("projectSave")]
    ProjectSave = 15,

    /// <summary> Runs an external process or shell. </summary>
    [UcliContractLiteral("externalProcess")]
    ExternalProcess = 16,

    /// <summary> Writes to the filesystem outside Unity Editor save boundaries. </summary>
    [UcliContractLiteral("filesystemWrite")]
    FilesystemWrite = 17,

    /// <summary> Executes source code supplied by the caller. </summary>
    [UcliContractLiteral("arbitrarySourceExecution")]
    ArbitrarySourceExecution = 18,

    /// <summary> Performs destructive operations whose target boundary is not sufficiently bounded. </summary>
    [UcliContractLiteral("destructiveScope")]
    DestructiveScope = 19,

    /// <summary> Mutates Play Mode runtime state without persisting Unity project resources. </summary>
    [UcliContractLiteral("runtimeStateMutation")]
    RuntimeStateMutation = 20,
}
