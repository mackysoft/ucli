namespace MackySoft.Ucli.Skills.Shared;

/// <summary> Normalizes SKILL text content for deterministic generation and digest input. </summary>
internal static class SkillTextNormalizer
{
    /// <summary> Converts CRLF and CR line endings to LF. </summary>
    /// <param name="text"> The text to normalize. </param>
    /// <returns> The LF-normalized text. </returns>
    public static string NormalizeToLf (string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
}
