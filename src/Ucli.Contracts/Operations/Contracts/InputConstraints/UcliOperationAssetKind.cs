using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported asset-kind constraint parameters. </summary>
public enum UcliOperationAssetKind
{
    /// <summary> No asset kind parameter is specified. </summary>
    [UcliContractLiteralIgnore]
    Unspecified = 0,

    /// <summary> Regular Unity asset. </summary>
    [UcliContractLiteral("asset")]
    Asset = 1,

    /// <summary> Unity prefab asset. </summary>
    [UcliContractLiteral("prefab")]
    Prefab = 2,

    /// <summary> Unity project settings asset. </summary>
    [UcliContractLiteral("projectSettings")]
    ProjectSettings = 3,

    /// <summary> Unity scene asset. </summary>
    [UcliContractLiteral("scene")]
    Scene = 4,
}
