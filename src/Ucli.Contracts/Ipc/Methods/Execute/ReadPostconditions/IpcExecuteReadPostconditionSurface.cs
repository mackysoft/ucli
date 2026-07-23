
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies a read-index surface affected by an <c>execute</c> mutation. </summary>
[VocabularyDefinition]
public enum IpcExecuteReadPostconditionSurface
{
    /// <summary> Indicates the asset-search lookup surface. </summary>
    [VocabularyText("assetSearch")]
    AssetSearch = 1,

    /// <summary> Indicates the GUID-path lookup surface. </summary>
    [VocabularyText("guidPath")]
    GuidPath = 2,

    /// <summary> Indicates the scene-tree-lite lookup surface. </summary>
    [VocabularyText("sceneTreeLite")]
    SceneTreeLite = 3,
}
