using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Normalizes the CLI <c>--testPlatform</c> option into a typed override. </summary>
internal static class TestRunPlatformOptionNormalizer
{
    /// <summary> Normalizes one optional <c>--testPlatform</c> value. </summary>
    /// <param name="optionValue"> The raw command option value. </param>
    /// <returns> The normalization result. </returns>
    public static TestRunPlatformOptionNormalizationResult Normalize (string? optionValue)
    {
        if (optionValue is null)
        {
            return TestRunPlatformOptionNormalizationResult.Omitted();
        }

        if (TestRunPlatformCodec.TryParse(optionValue, out var testPlatform))
        {
            return TestRunPlatformOptionNormalizationResult.Success(testPlatform);
        }

        return TestRunPlatformOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(
            $"testPlatform must not be empty. Actual: {optionValue}"));
    }
}