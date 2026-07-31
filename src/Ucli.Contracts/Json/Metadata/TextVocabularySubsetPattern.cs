using System.Text.RegularExpressions;

namespace MackySoft.Ucli.Contracts.Json.Metadata;

/// <summary> Projects a typed text-vocabulary subset as one exact string pattern. </summary>
internal static class TextVocabularySubsetPattern
{
    public static string Create<TEnum> (IEnumerable<TEnum> values)
        where TEnum : struct, Enum
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var texts = values
            .Select(GetEscapedText)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        if (texts.Length == 0)
        {
            throw new ArgumentException(
                "A text-vocabulary subset must contain at least one value.",
                nameof(values));
        }

        return "^(" + string.Join("|", texts) + ")$(?![\\s\\S])";
    }

    private static string GetEscapedText<TEnum> (TEnum value)
        where TEnum : struct, Enum
    {
        if (!TextVocabulary.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A text-vocabulary subset can contain only defined values.");
        }

        return Regex.Escape(TextVocabulary.GetText(value));
    }
}
