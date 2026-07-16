using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the closed set of IPC response outcomes. </summary>
public enum IpcResponseStatus
{
    /// <summary> Indicates that request processing completed successfully. </summary>
    [UcliContractLiteral("ok")]
    Ok = 1,

    /// <summary> Indicates that request processing failed with one or more errors. </summary>
    [UcliContractLiteral("error")]
    Error = 2,
}
