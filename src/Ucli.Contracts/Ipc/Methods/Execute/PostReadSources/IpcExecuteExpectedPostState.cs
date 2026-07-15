using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the post-state availability expected from an <c>execute</c> source fact. </summary>
public enum IpcExecuteExpectedPostState
{
    /// <summary> Indicates a deterministic post-state observation target. </summary>
    [UcliContractLiteral("deterministic")]
    Deterministic = 1,

    /// <summary> Indicates that the expected post-state cannot be derived from the source alone. </summary>
    [UcliContractLiteral("unavailable")]
    Unavailable = 2,
}
