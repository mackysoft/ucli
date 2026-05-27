using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported reference target-kind constraint parameters. </summary>
public enum UcliOperationReferenceTargetKind
{
    /// <summary> No target kind parameter is specified. </summary>
    [UcliContractLiteralIgnore]
    Unspecified = 0,

    /// <summary> Asset reference target. </summary>
    [UcliContractLiteral("asset")]
    Asset = 1,

    /// <summary> Component reference target. </summary>
    [UcliContractLiteral("component")]
    Component = 2,

    /// <summary> GameObject reference target. </summary>
    [UcliContractLiteral("gameObject")]
    GameObject = 3,
}
