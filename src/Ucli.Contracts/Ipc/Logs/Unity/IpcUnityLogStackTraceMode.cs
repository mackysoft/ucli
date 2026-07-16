using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Unity log stack-trace inclusion mode literals. </summary>
public enum IpcUnityLogStackTraceMode
{
    /// <summary> Suppresses stack traces. </summary>
    [UcliContractLiteral("none")]
    None = 1,

    /// <summary> Includes stack traces for error events only. </summary>
    [UcliContractLiteral("error")]
    Error = 2,

    /// <summary> Includes stack traces for all events. </summary>
    [UcliContractLiteral("all")]
    All = 3,
}
