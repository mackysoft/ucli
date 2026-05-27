using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Converts stream-entry output-format values to canonical literals. </summary>
internal static class CliStreamEntryFormatCodec
{
    private const string TextValue = "text";

    private const string JsonValue = "json";

    /// <summary> Tries to parse one output-format literal to a typed stream-entry format. </summary>
    public static bool TryParse (
        string? format,
        out CliStreamEntryFormat parsedFormat,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            parsedFormat = CliStreamEntryFormat.Text;
            errorMessage = null;
            return true;
        }

        var normalizedFormat = format.Trim();
        if (string.Equals(normalizedFormat, TextValue, StringComparison.OrdinalIgnoreCase))
        {
            parsedFormat = CliStreamEntryFormat.Text;
            errorMessage = null;
            return true;
        }

        if (string.Equals(normalizedFormat, JsonValue, StringComparison.OrdinalIgnoreCase))
        {
            parsedFormat = CliStreamEntryFormat.Json;
            errorMessage = null;
            return true;
        }

        parsedFormat = default;
        errorMessage = $"format must be one of: {TextValue}, {JsonValue}. Actual: {format}.";
        return false;
    }
}
