using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Converts daemon session owner-kind values to canonical contract literals. </summary>
public static class DaemonSessionOwnerKindCodec
{
    private static readonly (DaemonSessionOwnerKind Value, string Literal)[] Mappings =
    {
        (DaemonSessionOwnerKind.Cli, DaemonSessionOwnerKindValues.Cli),
        (DaemonSessionOwnerKind.User, DaemonSessionOwnerKindValues.User),
    };

    /// <summary> Converts one owner-kind enum value to a canonical contract literal. </summary>
    /// <param name="ownerKind"> The owner-kind enum value. </param>
    /// <returns> The canonical contract literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="ownerKind" /> is unsupported. </exception>
    public static string ToValue (DaemonSessionOwnerKind ownerKind)
    {
        return LiteralCodecUtilities.ToValue(
            ownerKind,
            Mappings,
            nameof(ownerKind),
            "Unsupported daemon session ownerKind.");
    }

    /// <summary> Tries to parse one raw owner-kind literal. </summary>
    /// <param name="value"> The optional raw literal. </param>
    /// <param name="ownerKind"> The parsed owner-kind enum value when parsing succeeds; otherwise default value. </param>
    /// <returns> <see langword="true" /> when one supported literal is parsed; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out DaemonSessionOwnerKind ownerKind)
    {
        return LiteralCodecUtilities.TryParseTrimmed(
            value,
            Mappings,
            StringComparison.Ordinal,
            out ownerKind);
    }
}
