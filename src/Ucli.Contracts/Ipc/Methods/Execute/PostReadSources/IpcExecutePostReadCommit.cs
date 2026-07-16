using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the persistence commit requested by an <c>execute</c> edit step. </summary>
public enum IpcExecutePostReadCommit
{
    /// <summary> Indicates that the source requested no persistence commit. </summary>
    [UcliContractLiteral("none")]
    None = 1,

    /// <summary> Indicates that the source requested a context-scoped commit. </summary>
    [UcliContractLiteral("context")]
    Context = 2,

    /// <summary> Indicates that the source requested a project-scoped commit. </summary>
    [UcliContractLiteral("project")]
    Project = 3,
}
