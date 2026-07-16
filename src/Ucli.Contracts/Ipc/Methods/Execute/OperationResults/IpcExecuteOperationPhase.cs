using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the final phase reached by an <c>execute</c> operation. </summary>
public enum IpcExecuteOperationPhase
{
    /// <summary> Indicates the validation phase. </summary>
    [UcliContractLiteral("validate")]
    Validate = 1,

    /// <summary> Indicates the planning phase. </summary>
    [UcliContractLiteral("plan")]
    Plan = 2,

    /// <summary> Indicates the call phase. </summary>
    [UcliContractLiteral("call")]
    Call = 3,

    /// <summary> Indicates a step that was skipped. </summary>
    [UcliContractLiteral("skipped")]
    Skipped = 4,
}
