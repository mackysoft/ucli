using System.Globalization;
using System.Text;

namespace MackySoft.Ucli.Hosting.Cli.Common.Streaming;

/// <summary> Sanitizes untrusted text values for physical single-line stream entries. </summary>
internal static class CliTextEntrySanitizer
{
    /// <summary> Converts one value to a terminal-safe single-line representation. </summary>
    public static string Sanitize (string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder? builder = null;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var replacement = GetReplacement(c);
            if (replacement is null)
            {
                builder?.Append(c);
                continue;
            }

            builder ??= new StringBuilder(value.Length + 8).Append(value, 0, i);
            builder.Append(replacement);
        }

        return builder?.ToString() ?? value;
    }

    private static string? GetReplacement (char c)
    {
        return c switch
        {
            '\r' => "\\r",
            '\n' => "\\n",
            '\t' => "\\t",
            '\u001B' => "\\u001B",
            _ when c < ' ' || c == '\u007F' || (c >= '\u0080' && c <= '\u009F') => "\\u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture),
            _ => null,
        };
    }
}
