using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Converts logs-command output-format values to canonical literals. </summary>
internal static class LogsOutputFormatCodec
{
    /// <summary> Gets the text output-format literal. </summary>
    public const string Text = CliStreamEntryFormatCodec.Text;

    /// <summary> Gets the JSON output-format literal. </summary>
    public const string Json = CliStreamEntryFormatCodec.Json;

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
        return CliStreamEntryFormatCodec.TryParse(format, out parsedFormat, out errorMessage);
    }
}
