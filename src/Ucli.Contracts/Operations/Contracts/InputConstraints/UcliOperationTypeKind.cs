using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported type-kind constraint parameters. </summary>
public enum UcliOperationTypeKind
{
    /// <summary> Unity component type. </summary>
    [UcliContractLiteral("component")]
    Component = 1,
}
