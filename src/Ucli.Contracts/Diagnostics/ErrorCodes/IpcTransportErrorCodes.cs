namespace MackySoft.Ucli.Contracts;

/// <summary> Defines IPC transport error code values. </summary>
public static class IpcTransportErrorCodes
{
    /// <summary> Gets the error code emitted when IPC execution exceeds the configured timeout budget. </summary>
    public static readonly UcliCode IpcTimeout = new("IPC_TIMEOUT");
}
