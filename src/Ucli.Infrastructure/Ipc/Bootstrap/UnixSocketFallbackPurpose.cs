namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Identifies one closed uCLI use of a deterministic Unix socket fallback path. </summary>
internal enum UnixSocketFallbackPurpose
{
    /// <summary> Identifies a Unity daemon endpoint. </summary>
    Daemon = 0,

    /// <summary> Identifies a Unity GUI-supervisor endpoint. </summary>
    GuiSupervisor = 1,

    /// <summary> Identifies a CLI supervisor endpoint. </summary>
    Supervisor = 2,

    /// <summary> Identifies a generation-specific CLI supervisor endpoint. </summary>
    SupervisorGeneration = 3,

    /// <summary> Identifies a CLI supervisor endpoint-publication lock. </summary>
    SupervisorPublicationLock = 4,

    /// <summary> Identifies a Unity listener endpoint-ownership lock. </summary>
    ListenerOwnershipLock = 5,
}
