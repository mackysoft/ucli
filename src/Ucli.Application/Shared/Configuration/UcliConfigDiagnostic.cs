using System.Globalization;
using System.Text;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Represents one diagnostic produced while validating or compiling <c>.ucli/config.json</c>. </summary>
internal sealed record UcliConfigDiagnostic
{
    internal const int MaxTextLength = 512;
    private const string TruncatedTextSuffix = "...";

    private UcliConfigDiagnostic (
        string code,
        string? propertyPath,
        string? sourcePath,
        string message)
    {
        Code = code;
        PropertyPath = propertyPath;
        SourcePath = sourcePath;
        Message = message;
    }

    /// <summary> Gets the stable diagnostic code. </summary>
    public string Code { get; }

    /// <summary> Gets the JSON property path related to the diagnostic, or <see langword="null" /> for document-level diagnostics. </summary>
    public string? PropertyPath { get; }

    /// <summary> Gets the source config path related to the diagnostic, or <see langword="null" /> when unavailable. </summary>
    public string? SourcePath { get; }

    /// <summary> Gets the user-facing diagnostic message. </summary>
    public string Message { get; }

    /// <summary> Creates a config diagnostic after validating required text fields. </summary>
    /// <param name="code"> The stable diagnostic code. </param>
    /// <param name="propertyPath"> The JSON property path related to the diagnostic. </param>
    /// <param name="sourcePath"> The source config path related to the diagnostic. </param>
    /// <param name="message"> The user-facing diagnostic message. </param>
    /// <returns> The created diagnostic. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="code" /> or <paramref name="message" /> is empty. </exception>
    public static UcliConfigDiagnostic Create (
        string code,
        string? propertyPath,
        string? sourcePath,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new UcliConfigDiagnostic(
            SanitizeRequiredText(code),
            SanitizeOptionalText(propertyPath),
            SanitizeOptionalText(sourcePath),
            SanitizeRequiredText(message));
    }

    /// <summary> Formats one untrusted diagnostic fragment before it is interpolated into a larger message. </summary>
    /// <param name="value"> The untrusted fragment value. </param>
    /// <returns> The escaped and length-limited fragment text. </returns>
    public static string FormatFragment (string? value)
    {
        return SanitizeRequiredText(value ?? "<null>");
    }

    private static string SanitizeRequiredText (string value)
    {
        return LimitLength(EscapeControlCharacters(value));
    }

    private static string? SanitizeOptionalText (string? value)
    {
        return value is null ? null : LimitLength(EscapeControlCharacters(value));
    }

    private static string EscapeControlCharacters (string value)
    {
        StringBuilder? builder = null;
        for (var i = 0; i < value.Length; i++)
        {
            var scalarCharLength = GetScalarCharLength(value, i);
            var category = CharUnicodeInfo.GetUnicodeCategory(value, i);
            if (!ShouldEscape(category))
            {
                if (builder is not null)
                {
                    builder.Append(value, i, scalarCharLength);
                }

                if (scalarCharLength > 1)
                {
                    i += scalarCharLength - 1;
                }

                continue;
            }

            builder ??= new StringBuilder(value.Length + 8);
            if (builder.Length == 0 && i > 0)
            {
                builder.Append(value, 0, i);
            }

            AppendEscapedUtf16(builder, value, i, scalarCharLength);
            if (scalarCharLength > 1)
            {
                i += scalarCharLength - 1;
            }
        }

        return builder?.ToString() ?? value;
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

    private static void AppendEscapedUtf16 (
        StringBuilder builder,
        string value,
        int startIndex,
        int charLength)
    {
        for (var i = 0; i < charLength; i++)
        {
            builder
                .Append("\\u")
                .Append(((int)value[startIndex + i]).ToString("X4", CultureInfo.InvariantCulture));
        }
    }

    private static bool ShouldEscape (UnicodeCategory category)
    {
        return category is UnicodeCategory.Control
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator
            or UnicodeCategory.Format;
    }

    private static string LimitLength (string value)
    {
        if (value.Length <= MaxTextLength)
        {
            return value;
        }

        return value[..(MaxTextLength - TruncatedTextSuffix.Length)] + TruncatedTextSuffix;
    }
}
