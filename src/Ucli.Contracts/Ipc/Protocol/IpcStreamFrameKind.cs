using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the closed set of IPC streaming frame shapes. </summary>
public enum IpcStreamFrameKind
{
    /// <summary> Carries one non-terminal progress event. </summary>
    [UcliContractLiteral("progress")]
    Progress = 1,

    /// <summary> Carries the terminal IPC response. </summary>
    [UcliContractLiteral("terminal")]
    Terminal = 2,
}
