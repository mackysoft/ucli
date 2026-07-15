using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies the strongest diagnostic impact projected into verify post-read evidence. </summary>
public enum VerifyDiagnosticImpact
{
    /// <summary> Diagnostics do not affect the claim result. </summary>
    [UcliContractLiteral("none")]
    None = 1,

    /// <summary> Diagnostics reduce claim coverage to a partial result. </summary>
    [UcliContractLiteral("partial")]
    Partial = 2,

    /// <summary> Diagnostics prevent coverage from being determined. </summary>
    [UcliContractLiteral("indeterminate")]
    Indeterminate = 3,

    /// <summary> An error diagnostic fails the claim. </summary>
    [UcliContractLiteral("error")]
    Error = 4,
}
