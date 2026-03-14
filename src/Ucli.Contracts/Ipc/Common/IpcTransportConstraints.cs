namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines transport-level constraints shared by IPC endpoint resolution and tests. </summary>
public static class IpcTransportConstraints
{
    /// <summary> Gets the maximum usable UTF-8 byte length for Unix domain socket paths on macOS. </summary>
    public const int UnixDomainSocketPathMaxBytes = 103;
}