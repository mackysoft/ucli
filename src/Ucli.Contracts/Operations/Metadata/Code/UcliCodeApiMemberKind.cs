using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Identifies the shape of a source-facing API member. </summary>
public enum UcliCodeApiMemberKind
{
    /// <summary> Indicates a property member. </summary>
    [UcliContractLiteral("property")]
    Property = 1,

    /// <summary> Indicates a method member. </summary>
    [UcliContractLiteral("method")]
    Method = 2,
}
