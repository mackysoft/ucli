namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines canonical endpoint naming literals shared by uCLI IPC transports. </summary>
public static class UcliIpcEndpointNames
{
    /// <summary> Gets the daemon endpoint prefix used for named pipes and unix fallback directories. </summary>
    public const string DaemonAddressPrefix = "ucli-daemon-";

    /// <summary> Gets the supervisor endpoint prefix used for named pipes and unix fallback directories. </summary>
    public const string SupervisorAddressPrefix = "ucli-supervisor-";

    /// <summary> Gets the GUI supervisor endpoint prefix used for named pipes and unix fallback directories. </summary>
    public const string GuiSupervisorAddressPrefix = "ucli-gui-supervisor-";

    /// <summary> Gets the fixed unix-domain-socket file name used by uCLI listeners. </summary>
    public const string UnixSocketFileName = "ipc.sock";
}
