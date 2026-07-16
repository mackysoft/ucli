using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the level of a log entry emitted by evaluated C# code. </summary>
public enum CsEvalLogLevel
{
    /// <summary> Indicates an informational log entry. </summary>
    [UcliContractLiteral("log")]
    Log = 1,

    /// <summary> Indicates a warning log entry. </summary>
    [UcliContractLiteral("warning")]
    Warning = 2,

    /// <summary> Indicates an error log entry. </summary>
    [UcliContractLiteral("error")]
    Error = 3,
}
