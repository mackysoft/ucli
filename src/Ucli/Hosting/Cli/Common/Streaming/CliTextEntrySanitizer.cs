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
        for (var i = 0; i < value.Length;)
        {
            var replacement = GetReplacement(value, i, out var charLength);
            if (replacement is null)
            {
                builder?.Append(value, i, charLength);
                i += charLength;
                continue;
            }

            builder ??= new StringBuilder(value.Length + 8).Append(value, 0, i);
            builder.Append(replacement);
            i += charLength;
        }

        return builder?.ToString() ?? value;
    }

    private static string? GetReplacement (
        string value,
        int index,
        out int charLength)
    {
        charLength = GetScalarCharLength(value, index);
        var c = value[index];
        if (charLength == 1)
        {
            var controlReplacement = c switch
            {
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                '\u001B' => "\\u001B",
                _ => null,
            };
            if (controlReplacement is not null)
            {
                return controlReplacement;
            }
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(value, index);
        return ShouldEscape(category)
            ? EscapeUtf16(value, index, charLength)
            : null;
    }

    private static int GetScalarCharLength (
        string value,
        int index)
    {
        return char.IsHighSurrogate(value[index])
            && index + 1 < value.Length
            && char.IsLowSurrogate(value[index + 1])
            ? 2
            : 1;
    }

    private static bool ShouldEscape (UnicodeCategory category)
    {
        return category switch
        {
            UnicodeCategory.Control
                or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator
                or UnicodeCategory.Surrogate => true,
            _ => false,
        };
    }

    private static string EscapeUtf16 (
        string value,
        int index,
        int charLength)
    {
        var builder = new StringBuilder(charLength * 6);
        for (var i = 0; i < charLength; i++)
        {
            builder.Append("\\u");
            builder.Append(((int)value[index + i]).ToString("X4", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
