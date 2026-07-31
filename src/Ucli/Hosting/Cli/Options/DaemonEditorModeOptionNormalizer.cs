using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes the CLI <c>--editorMode</c> option into a daemon Editor mode literal. </summary>
internal static class UnityEditorModeOptionNormalizer
{
    /// <summary> Normalizes one optional <c>--editorMode</c> value. </summary>
    /// <param name="optionValue"> The raw command option value. </param>
    /// <returns> The normalization result. </returns>
    public static UnityEditorModeOptionNormalizationResult Normalize (string? optionValue)
    {
        if (optionValue is null)
        {
            return UnityEditorModeOptionNormalizationResult.Success(editorMode: null);
        }

        if (VocabularyInputParser.TryParseTrimmed<UnityEditorMode>(optionValue, out var editorMode))
        {
            return UnityEditorModeOptionNormalizationResult.Success(editorMode);
        }

        return UnityEditorModeOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(
            $"editorMode must be one of '{TextVocabulary.GetText(UnityEditorMode.Batchmode)}', '{TextVocabulary.GetText(UnityEditorMode.Gui)}'. Actual: {optionValue}."));
    }
}
