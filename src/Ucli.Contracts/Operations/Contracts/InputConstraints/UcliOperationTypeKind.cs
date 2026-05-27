using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported type-kind constraint parameters. </summary>
public enum UcliOperationTypeKind
{
    /// <summary> No type kind parameter is specified. </summary>
    [UcliContractLiteralIgnore]
    Unspecified = 0,

    /// <summary> Unity component type. </summary>
    [UcliContractLiteral("component")]
    Component = 1,
}
