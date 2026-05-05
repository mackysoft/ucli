using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Represents the result of typed-query common option normalization. </summary>
internal sealed record QueryCommonOptionsNormalizationResult (
    QueryCommonOptions? Options,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether normalization succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates one successful normalization result. </summary>
    public static QueryCommonOptionsNormalizationResult Success (QueryCommonOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new QueryCommonOptionsNormalizationResult(options, null);
    }

    /// <summary> Creates one failed normalization result. </summary>
    public static QueryCommonOptionsNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new QueryCommonOptionsNormalizationResult(null, error);
    }
}
