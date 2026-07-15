using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Identifies the semantic role of a code catalog entry. </summary>
internal enum CodeCatalogKind
{
    /// <summary> A command failure code emitted at <c>errors[].code</c>. </summary>
    [UcliContractLiteral("error")]
    Error = 1,

    /// <summary> A diagnostic evidence code. </summary>
    [UcliContractLiteral("diagnostic")]
    Diagnostic = 2,

    /// <summary> A machine-readable reason code. </summary>
    [UcliContractLiteral("reason")]
    Reason = 3,

    /// <summary> An assurance claim code. </summary>
    [UcliContractLiteral("claim")]
    Claim = 4,

    /// <summary> A residual risk code. </summary>
    [UcliContractLiteral("risk")]
    Risk = 5,

    /// <summary> A code that is not registered in the local catalog. </summary>
    [UcliContractLiteral("unknown")]
    Unknown = 6,
}
