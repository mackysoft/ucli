using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies how an <c>execute</c> diagnostic affects coverage. </summary>
public enum IpcExecuteDiagnosticCoverageImpact
{
    /// <summary> Indicates that the diagnostic has no coverage impact. </summary>
    [UcliContractLiteral("none")]
    None = 1,

    /// <summary> Indicates that the operation covered only part of the requested target set. </summary>
    [UcliContractLiteral("partial")]
    Partial = 2,

    /// <summary> Indicates that coverage could not be determined. </summary>
    [UcliContractLiteral("indeterminate")]
    Indeterminate = 3,
}
