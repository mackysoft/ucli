namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines supported IPC method names. </summary>
public static class IpcMethodNames
{
    /// <summary> Gets the method name used for connectivity checks. </summary>
    public const string Ping = "ping";

    /// <summary> Gets the method name used for Unity command execution requests. </summary>
    public const string Execute = "execute";

    /// <summary> Gets the method name used for daemon shutdown requests. </summary>
    public const string Shutdown = "shutdown";
}