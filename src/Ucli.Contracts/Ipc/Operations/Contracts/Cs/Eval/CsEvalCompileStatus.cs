using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the result of compiling C# eval source. </summary>
public enum CsEvalCompileStatus
{
    /// <summary> Indicates that compilation and entry-point validation succeeded. </summary>
    [UcliContractLiteral("succeeded")]
    Succeeded = 1,

    /// <summary> Indicates that compilation or entry-point validation failed. </summary>
    [UcliContractLiteral("failed")]
    Failed = 2,
}
