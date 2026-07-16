namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines address limits shared by IPC endpoint producers and consumers. </summary>
internal static class IpcTransportConstraints
{
    /// <summary> Gets the maximum named pipe name length after excluding the Windows pipe namespace prefix. </summary>
    public const int NamedPipeAddressMaxCharacters = 247;

    /// <summary> Gets the conservative UTF-8 byte limit supported for Unix domain socket paths by all supported runtimes. </summary>
    public const int UnixDomainSocketPathMaxBytes = 102;
}
