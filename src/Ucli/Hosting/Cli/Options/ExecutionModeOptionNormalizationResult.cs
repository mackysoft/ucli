using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Represents one normalization result for the <c>--mode</c> option. </summary>
internal sealed record ExecutionModeOptionNormalizationResult (
    bool IsSpecified,
    UnityExecutionMode? Mode,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the option was normalized successfully. </summary>
    public bool IsSuccess => Error == null;

    /// <summary> Creates a normalization result for an omitted option. </summary>
    /// <returns> The omitted-option result. </returns>
    public static ExecutionModeOptionNormalizationResult Omitted ()
    {
        return new ExecutionModeOptionNormalizationResult(
            IsSpecified: false,
            Mode: null,
            Error: null);
    }

    /// <summary> Creates a successful normalization result. </summary>
    /// <param name="mode"> The normalized mode override. </param>
    /// <returns> The successful result. </returns>
    public static ExecutionModeOptionNormalizationResult Success (UnityExecutionMode mode)
    {
        return new ExecutionModeOptionNormalizationResult(
            IsSpecified: true,
            Mode: mode,
            Error: null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    /// <param name="error"> The normalization error. </param>
    /// <returns> The failed result. </returns>
    public static ExecutionModeOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ExecutionModeOptionNormalizationResult(
            IsSpecified: true,
            Mode: null,
            Error: error);
    }
}