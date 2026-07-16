using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies a read-index surface affected by an <c>execute</c> mutation. </summary>
public enum IpcExecuteReadPostconditionSurface
{
    /// <summary> Indicates the asset-search lookup surface. </summary>
    [UcliContractLiteral("assetSearch")]
    AssetSearch = 1,

    /// <summary> Indicates the GUID-path lookup surface. </summary>
    [UcliContractLiteral("guidPath")]
    GuidPath = 2,

    /// <summary> Indicates the scene-tree-lite lookup surface. </summary>
    [UcliContractLiteral("sceneTreeLite")]
    SceneTreeLite = 3,
}
