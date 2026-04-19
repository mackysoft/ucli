using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Represents one normalization result for the <c>--readIndexMode</c> option. </summary>
internal sealed record ReadIndexModeOptionNormalizationResult (
    ReadIndexMode? Mode,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the option was normalized successfully. </summary>
    public bool IsSuccess => Error == null;

    /// <summary> Creates a successful normalization result. </summary>
    /// <param name="mode"> The normalized mode override, or <see langword="null" /> when the option was omitted. </param>
    /// <returns> The successful result. </returns>
    public static ReadIndexModeOptionNormalizationResult Success (ReadIndexMode? mode)
    {
        return new ReadIndexModeOptionNormalizationResult(mode, null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    /// <param name="error"> The normalization error. </param>
    /// <returns> The failed result. </returns>
    public static ReadIndexModeOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ReadIndexModeOptionNormalizationResult(null, error);
    }
}