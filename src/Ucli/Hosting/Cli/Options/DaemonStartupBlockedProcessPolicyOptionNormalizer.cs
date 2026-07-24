using MackySoft.Ucli.Application.Shared.Foundation;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes the CLI <c>--onStartupBlocked</c> option into a daemon startup-blocked process policy. </summary>
internal static class DaemonStartupBlockedProcessPolicyOptionNormalizer
{
    /// <summary> Normalizes one optional <c>--onStartupBlocked</c> value. </summary>
    /// <param name="optionValue"> The raw command option value. </param>
    /// <returns> The normalization result. </returns>
    public static DaemonStartupBlockedProcessPolicyOptionNormalizationResult Normalize (string? optionValue)
    {
        if (optionValue is null)
        {
            return DaemonStartupBlockedProcessPolicyOptionNormalizationResult.Success(
                DaemonStartupBlockedProcessPolicy.Auto);
        }

        if (VocabularyInputParser.TryParseTrimmed<DaemonStartupBlockedProcessPolicy>(optionValue, out var policy))
        {
            return DaemonStartupBlockedProcessPolicyOptionNormalizationResult.Success(policy);
        }

        return DaemonStartupBlockedProcessPolicyOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(
            $"onStartupBlocked must be one of '{TextVocabulary.GetText(DaemonStartupBlockedProcessPolicy.Auto)}', " +
            $"'{TextVocabulary.GetText(DaemonStartupBlockedProcessPolicy.Keep)}', '{TextVocabulary.GetText(DaemonStartupBlockedProcessPolicy.Terminate)}'. " +
            $"Actual: {optionValue}."));
    }
}
