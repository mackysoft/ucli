using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Identifies the severity of a structured uCLI diagnostic. </summary>
public enum UcliDiagnosticSeverity
{
    /// <summary> Indicates an informational diagnostic. </summary>
    [UcliContractLiteral("info")]
    Info = 1,

    /// <summary> Indicates a warning diagnostic. </summary>
    [UcliContractLiteral("warning")]
    Warning = 2,

    /// <summary> Indicates an error diagnostic. </summary>
    [UcliContractLiteral("error")]
    Error = 3,
}
