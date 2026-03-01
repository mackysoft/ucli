namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines canonical string literals for IPC endpoint transport kinds. </summary>
public static class IpcTransportKindValues
{
    /// <summary> Gets the transport kind value used for named-pipe endpoints. </summary>
    public const string NamedPipe = "namedPipe";

    /// <summary> Gets the transport kind value used for unix-domain-socket endpoints. </summary>
    public const string UnixDomainSocket = "unixDomainSocket";

    /// <summary> Determines whether one transport kind value is supported by the IPC contract. </summary>
    /// <param name="value"> The transport kind value. </param>
    /// <returns> <see langword="true" /> when value is supported; otherwise <see langword="false" />. </returns>
    public static bool IsSupported (string value)
    {
        return string.Equals(value, NamedPipe, System.StringComparison.Ordinal)
            || string.Equals(value, UnixDomainSocket, System.StringComparison.Ordinal);
    }
}