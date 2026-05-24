namespace MackySoft.Ucli.Hosting.Cli.Common.Streaming;

/// <summary> Converts stream-entry output-format values to canonical literals. </summary>
internal static class CliStreamEntryFormatCodec
{
    /// <summary> Gets the text output-format literal. </summary>
    public const string Text = "text";

    /// <summary> Gets the JSON output-format literal. </summary>
    public const string Json = "json";

    /// <summary> Tries to parse one output-format literal to canonical value. </summary>
    public static bool TryParse (
        string? format,
        out string? parsedFormat,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            parsedFormat = Text;
            errorMessage = null;
            return true;
        }

        if (string.Equals(format, Text, StringComparison.OrdinalIgnoreCase))
        {
            parsedFormat = Text;
            errorMessage = null;
            return true;
        }

        if (string.Equals(format, Json, StringComparison.OrdinalIgnoreCase))
        {
            parsedFormat = Json;
            errorMessage = null;
            return true;
        }

        parsedFormat = null;
        errorMessage = $"format must be one of: {Text}, {Json}. Actual: {format}.";
        return false;
    }
}
