namespace MackySoft.Ucli.Hosting.Cli;

/// <summary> Converts logs-command output-format values to canonical literals. </summary>
internal static class LogsOutputFormatCodec
{
    /// <summary> Gets the text output-format literal. </summary>
    public const string Text = "text";

    /// <summary> Gets the JSON output-format literal. </summary>
    public const string Json = "json";

    /// <summary> Tries to parse one output-format literal to canonical value. </summary>
    /// <param name="format"> The raw output-format value. </param>
    /// <param name="parsedFormat"> The normalized output-format when parse succeeds. </param>
    /// <param name="errorMessage"> The invalid-argument error message when parse fails. </param>
    /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
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