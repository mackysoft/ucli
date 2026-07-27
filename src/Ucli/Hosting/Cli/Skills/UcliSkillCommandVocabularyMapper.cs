using MackySoft.AgentSkills.Shared.Text;

namespace MackySoft.Ucli.Hosting.Cli.Skills;

/// <summary>
/// Converts Agent Skills runtime literals into the typed vocabulary contracts emitted by uCLI.
/// </summary>
internal static class UcliSkillCommandVocabularyMapper
{
    public static TTarget Map<TSource, TTarget> (TSource value)
        where TSource : struct, Enum
        where TTarget : struct, Enum
    {
        return Parse<TTarget>(
            ContractLiteralCodec.ToValue(value),
            typeof(TSource).Name);
    }

    public static TTarget Parse<TTarget> (
        string text,
        string sourceName)
        where TTarget : struct, Enum
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        if (TextVocabulary.TryGetValue<TTarget>(text, out var value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"{sourceName} returned unsupported {typeof(TTarget).Name} text: {text}.");
    }
}
