using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Identifies a read-index artifact represented by ready assurance evidence. </summary>
internal enum ReadyReadIndexArtifactName
{
    /// <summary> The read-index mode validation result. </summary>
    [UcliContractLiteral("readIndex.mode")]
    Mode = 1,

    /// <summary> The operation catalog artifact. </summary>
    [UcliContractLiteral("ops.catalog")]
    OpsCatalog = 2,

    /// <summary> The asset-search lookup artifact. </summary>
    [UcliContractLiteral("asset-search.lookup")]
    AssetSearchLookup = 3,

    /// <summary> The GUID-to-path lookup artifact. </summary>
    [UcliContractLiteral("guid-path.lookup")]
    GuidPathLookup = 4,
}
