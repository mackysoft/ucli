using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Converts read-index mode values between enum and contract literals. </summary>
public static class ReadIndexModeCodec
{
    private static readonly (ReadIndexMode Value, string Literal)[] Mappings =
    {
        (ReadIndexMode.Disabled, ReadIndexModeValues.Disabled),
        (ReadIndexMode.AllowStale, ReadIndexModeValues.AllowStale),
        (ReadIndexMode.RequireFresh, ReadIndexModeValues.RequireFresh),
    };

    /// <summary> Converts one read-index mode enum value to config literal. </summary>
    /// <param name="readIndexMode"> The read-index mode enum value. </param>
    /// <returns> The config literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="readIndexMode" /> is unsupported. </exception>
    public static string ToValue (ReadIndexMode readIndexMode)
    {
        return LiteralCodecUtilities.ToValue(
            readIndexMode,
            Mappings,
            nameof(readIndexMode),
            "Unsupported readIndexMode.");
    }

    /// <summary> Tries to parse config literal to read-index mode enum. </summary>
    /// <param name="value"> The config literal value. </param>
    /// <param name="readIndexMode"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out ReadIndexMode readIndexMode)
    {
        return LiteralCodecUtilities.TryParse(
            value,
            Mappings,
            StringComparison.OrdinalIgnoreCase,
            out readIndexMode);
    }
}