namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Provides text-output helpers shared by logs commands. </summary>
internal static class LogsTextUtilities
{
    /// <summary> Normalizes one text value into a single-line representation. </summary>
    /// <param name="value"> The source text. </param>
    /// <returns> Single-line normalized text. </returns>
    public static string NormalizeSingleLine (string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}