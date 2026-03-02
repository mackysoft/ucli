namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Converts read-index mode values between enum and contract literals. </summary>
public static class ReadIndexModeCodec
{
    /// <summary> Converts one read-index mode enum value to config literal. </summary>
    /// <param name="readIndexMode"> The read-index mode enum value. </param>
    /// <returns> The config literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="readIndexMode" /> is unsupported. </exception>
    public static string ToValue (ReadIndexMode readIndexMode)
    {
        return readIndexMode switch
        {
            ReadIndexMode.Disabled => ReadIndexModeValues.Disabled,
            ReadIndexMode.AllowStale => ReadIndexModeValues.AllowStale,
            ReadIndexMode.RequireFresh => ReadIndexModeValues.RequireFresh,
            _ => throw new ArgumentOutOfRangeException(nameof(readIndexMode), readIndexMode, "Unsupported readIndexMode."),
        };
    }

    /// <summary> Tries to parse config literal to read-index mode enum. </summary>
    /// <param name="value"> The config literal value. </param>
    /// <param name="readIndexMode"> The parsed enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out ReadIndexMode readIndexMode)
    {
        if (string.Equals(value, ReadIndexModeValues.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            readIndexMode = ReadIndexMode.Disabled;
            return true;
        }

        if (string.Equals(value, ReadIndexModeValues.AllowStale, StringComparison.OrdinalIgnoreCase))
        {
            readIndexMode = ReadIndexMode.AllowStale;
            return true;
        }

        if (string.Equals(value, ReadIndexModeValues.RequireFresh, StringComparison.OrdinalIgnoreCase))
        {
            readIndexMode = ReadIndexMode.RequireFresh;
            return true;
        }

        readIndexMode = default;
        return false;
    }
}