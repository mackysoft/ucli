using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts transport-kind values between enum and contract literals. </summary>
public static class IpcTransportKindCodec
{
    private static readonly (IpcTransportKind Value, string Literal)[] Mappings =
    {
        (IpcTransportKind.NamedPipe, IpcTransportKindValues.NamedPipe),
        (IpcTransportKind.UnixDomainSocket, IpcTransportKindValues.UnixDomainSocket),
    };

    /// <summary> Converts one transport enum value to contract literal. </summary>
    /// <param name="transportKind"> The transport enum value. </param>
    /// <returns> The transport literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="transportKind" /> is unsupported. </exception>
    public static string ToValue (IpcTransportKind transportKind)
    {
        return LiteralCodecUtilities.ToValue(
            transportKind,
            Mappings,
            nameof(transportKind),
            "Unsupported transport kind.");
    }

    /// <summary> Tries to parse contract literal to transport enum value. </summary>
    /// <param name="value"> The transport literal value. </param>
    /// <param name="transportKind"> The parsed transport enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out IpcTransportKind transportKind)
    {
        return LiteralCodecUtilities.TryParse(
            value,
            Mappings,
            StringComparison.Ordinal,
            out transportKind);
    }
}