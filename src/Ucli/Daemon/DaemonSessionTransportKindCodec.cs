using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Ipc;

namespace MackySoft.Ucli.Daemon;

/// <summary> Converts daemon session endpoint transport values between enum and contract literals. </summary>
internal static class DaemonSessionTransportKindCodec
{
    /// <summary> Converts one transport enum value to daemon session literal. </summary>
    /// <param name="transportKind"> The transport enum value. </param>
    /// <returns> The daemon session literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="transportKind" /> is unsupported. </exception>
    public static string ToValue (IpcTransportKind transportKind)
    {
        return transportKind switch
        {
            IpcTransportKind.NamedPipe => IpcTransportKindValues.NamedPipe,
            IpcTransportKind.UnixDomainSocket => IpcTransportKindValues.UnixDomainSocket,
            _ => throw new ArgumentOutOfRangeException(nameof(transportKind), transportKind, "Unsupported transport kind."),
        };
    }

    /// <summary> Tries to parse daemon session literal value to transport enum. </summary>
    /// <param name="value"> The daemon session literal value. </param>
    /// <param name="transportKind"> The parsed transport enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string value,
        out IpcTransportKind transportKind)
    {
        if (string.Equals(value, IpcTransportKindValues.NamedPipe, StringComparison.Ordinal))
        {
            transportKind = IpcTransportKind.NamedPipe;
            return true;
        }

        if (string.Equals(value, IpcTransportKindValues.UnixDomainSocket, StringComparison.Ordinal))
        {
            transportKind = IpcTransportKind.UnixDomainSocket;
            return true;
        }

        transportKind = default;
        return false;
    }
}