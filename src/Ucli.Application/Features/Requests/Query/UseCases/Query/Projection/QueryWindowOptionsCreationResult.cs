using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query.Projection;

/// <summary> Represents the result of query window option normalization. </summary>
internal sealed record QueryWindowOptionsCreationResult (
    QueryWindowOptions? Options,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether normalization succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Creates one successful normalization result. </summary>
    public static QueryWindowOptionsCreationResult Success (QueryWindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new QueryWindowOptionsCreationResult(options, null);
    }

    /// <summary> Creates one failed normalization result. </summary>
    public static QueryWindowOptionsCreationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new QueryWindowOptionsCreationResult(null, error);
    }
}
