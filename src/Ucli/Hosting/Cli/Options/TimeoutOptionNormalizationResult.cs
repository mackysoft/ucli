using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Represents one normalization result for the <c>--timeout</c> option. </summary>
internal sealed record TimeoutOptionNormalizationResult (
    int? TimeoutMilliseconds,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the option was normalized successfully. </summary>
    public bool IsSuccess => Error == null;

    /// <summary> Creates a successful normalization result. </summary>
    /// <param name="timeoutMilliseconds"> The normalized timeout override in milliseconds, or <see langword="null" /> when the option was omitted. </param>
    /// <returns> The successful result. </returns>
    public static TimeoutOptionNormalizationResult Success (int? timeoutMilliseconds)
    {
        return new TimeoutOptionNormalizationResult(timeoutMilliseconds, null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    /// <param name="error"> The normalization error. </param>
    /// <returns> The failed result. </returns>
    public static TimeoutOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new TimeoutOptionNormalizationResult(null, error);
    }
}
