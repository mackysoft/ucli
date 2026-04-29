namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines shared IPC protocol constants. </summary>
public static class IpcProtocol
{
    /// <summary> Gets the current IPC protocol major version. </summary>
    public const int CurrentVersion = 1;

    /// <summary> Gets the status literal that represents successful processing. </summary>
    public const string StatusOk = "ok";

    /// <summary> Gets the status literal that represents failed processing. </summary>
    public const string StatusError = "error";
}
