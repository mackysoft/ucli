using MackySoft.Ucli.Application.Shared.Foundation;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes the CLI <c>--editorMode</c> option into a daemon Editor mode literal. </summary>
internal static class DaemonEditorModeOptionNormalizer
{
    /// <summary> Normalizes one optional <c>--editorMode</c> value. </summary>
    /// <param name="optionValue"> The raw command option value. </param>
    /// <returns> The normalization result. </returns>
    public static DaemonEditorModeOptionNormalizationResult Normalize (string? optionValue)
    {
        if (optionValue is null)
        {
            return DaemonEditorModeOptionNormalizationResult.Success(editorMode: null);
        }

        if (ContractLiteralInputParser.TryParseTrimmed<DaemonEditorMode>(optionValue, out var editorMode))
        {
            return DaemonEditorModeOptionNormalizationResult.Success(editorMode);
        }

        return DaemonEditorModeOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(
            $"editorMode must be one of '{ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode)}', '{ContractLiteralCodec.ToValue(DaemonEditorMode.Gui)}'. Actual: {optionValue}."));
    }
}
