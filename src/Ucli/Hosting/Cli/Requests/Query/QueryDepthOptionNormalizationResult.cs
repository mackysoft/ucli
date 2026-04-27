using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Represents typed-query depth option normalization output. </summary>
internal sealed record QueryDepthOptionNormalizationResult (
    int? Depth,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether normalization succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates one successful depth normalization result. </summary>
    public static QueryDepthOptionNormalizationResult Success (int? depth)
    {
        return new QueryDepthOptionNormalizationResult(depth, null);
    }

    /// <summary> Creates one failed depth normalization result. </summary>
    public static QueryDepthOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new QueryDepthOptionNormalizationResult(null, error);
    }
}
