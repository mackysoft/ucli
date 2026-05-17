namespace MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;

/// <summary> Defines CLI-host specific machine-readable error code values. </summary>
internal static class ExecutionErrorCodes
{
    /// <summary> Gets the error code used when command execution is canceled. </summary>
    public static readonly UcliCode Canceled = new UcliCode("CANCELED");

    /// <summary> Gets the error code used when IPC execution exceeds configured timeout. </summary>
    public static readonly UcliCode IpcTimeout = IpcTransportErrorCodes.IpcTimeout;
}
