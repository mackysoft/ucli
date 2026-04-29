using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Represents one normalization result for the <c>--testPlatform</c> option. </summary>
internal sealed record TestRunPlatformOptionNormalizationResult (
    bool IsSpecified,
    TestRunPlatform? TestPlatform,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the option was normalized successfully. </summary>
    public bool IsSuccess => Error == null;

    /// <summary> Creates a normalization result for an omitted option. </summary>
    /// <returns> The omitted-option result. </returns>
    public static TestRunPlatformOptionNormalizationResult Omitted ()
    {
        return new TestRunPlatformOptionNormalizationResult(
            IsSpecified: false,
            TestPlatform: null,
            Error: null);
    }

    /// <summary> Creates a successful normalization result. </summary>
    /// <param name="testPlatform"> The normalized test-platform override. </param>
    /// <returns> The successful result. </returns>
    public static TestRunPlatformOptionNormalizationResult Success (TestRunPlatform testPlatform)
    {
        return new TestRunPlatformOptionNormalizationResult(
            IsSpecified: true,
            TestPlatform: testPlatform,
            Error: null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    /// <param name="error"> The normalization error. </param>
    /// <returns> The failed result. </returns>
    public static TestRunPlatformOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new TestRunPlatformOptionNormalizationResult(
            IsSpecified: true,
            TestPlatform: null,
            Error: error);
    }
}
