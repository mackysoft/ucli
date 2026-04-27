using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Normalizes typed-query depth options. </summary>
internal static class QueryDepthOptionNormalizer
{
    /// <summary> Normalizes one optional depth value and <c>--fullDepth</c>. </summary>
    public static QueryDepthOptionNormalizationResult Normalize (
        int? depth,
        bool fullDepth,
        int defaultDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(defaultDepth);

        if (fullDepth && depth.HasValue)
        {
            return QueryDepthOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(
                "'--fullDepth' cannot be combined with '--depth'."));
        }

        if (depth.HasValue && depth.Value < 0)
        {
            return QueryDepthOptionNormalizationResult.Failure(ExecutionError.InvalidArgument(
                $"depth must be greater than or equal to 0. Actual: {depth.Value}."));
        }

        return QueryDepthOptionNormalizationResult.Success(fullDepth ? null : depth ?? defaultDepth);
    }
}
