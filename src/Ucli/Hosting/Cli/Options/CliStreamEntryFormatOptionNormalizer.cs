using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes stream-entry <c>--format</c> options into typed rendering formats. </summary>
internal static class CliStreamEntryFormatOptionNormalizer
{
    /// <summary> Normalizes one optional stream-entry format value. </summary>
    public static CliStreamEntryFormatOptionNormalizationResult Normalize (string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return CliStreamEntryFormatOptionNormalizationResult.Success(CliStreamEntryFormat.Text);
        }

        if (VocabularyInputParser.TryParseIgnoreCase(format.Trim(), out CliStreamEntryFormat parsedFormat))
        {
            return CliStreamEntryFormatOptionNormalizationResult.Success(parsedFormat);
        }

        var errorMessage =
            $"format must be one of: {string.Join(", ", TextVocabulary.GetTexts<CliStreamEntryFormat>())}. Actual: {format}.";
        return CliStreamEntryFormatOptionNormalizationResult.Failure(
            ExecutionError.InvalidArgument(errorMessage));
    }
}
