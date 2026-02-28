namespace MackySoft.Ucli.Ipc;

/// <summary> Defines transport-level constraints shared by IPC endpoint resolution and tests. </summary>
internal static class IpcTransportConstraints
{
    /// <summary> Gets the maximum usable UTF-8 byte length for Unix domain socket paths on macOS. </summary>
    internal const int UnixDomainSocketPathMaxBytes = 103;
}