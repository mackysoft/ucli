using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported serialized-property access constraint parameters. </summary>
public enum UcliOperationSerializedPropertyAccess
{
    /// <summary> No serialized-property access parameter is specified. </summary>
    [UcliContractLiteralIgnore]
    Unspecified = 0,

    /// <summary> Write access. </summary>
    [UcliContractLiteral("write")]
    Write = 1,
}
