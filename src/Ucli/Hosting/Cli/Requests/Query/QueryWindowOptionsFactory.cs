using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates normalized query result windowing options from CLI values. </summary>
internal static class QueryWindowOptionsFactory
{
    /// <summary> Gets the default bounded query result limit. </summary>
    public const int DefaultLimit = 100;

    /// <summary> Gets the maximum bounded query result limit. </summary>
    public const int MaxLimit = 10000;

    /// <summary> Attempts to create normalized window options. </summary>
    public static QueryWindowOptionsCreationResult Create (
        bool all,
        int? limit,
        string? after)
    {
        if (all && (limit.HasValue || after is not null))
        {
            return QueryWindowOptionsCreationResult.Failure(ExecutionError.InvalidArgument(
                "'--all' cannot be combined with '--limit' or '--after'."));
        }

        if (all)
        {
            return QueryWindowOptionsCreationResult.Success(new QueryWindowOptions(
                All: true,
                Limit: 0,
                After: null,
                Offset: 0));
        }

        var normalizedLimit = limit ?? DefaultLimit;
        if (normalizedLimit < 1 || normalizedLimit > MaxLimit)
        {
            return QueryWindowOptionsCreationResult.Failure(ExecutionError.InvalidArgument(
                $"limit must be between 1 and {MaxLimit}. Actual: {normalizedLimit}."));
        }

        var offset = 0;
        if (after is not null
            && !QueryWindowCursorCodec.TryDecode(after, out offset))
        {
            return QueryWindowOptionsCreationResult.Failure(ExecutionError.InvalidArgument(
                "after cursor is invalid."));
        }

        return QueryWindowOptionsCreationResult.Success(new QueryWindowOptions(
            All: false,
            Limit: normalizedLimit,
            After: after,
            Offset: offset));
    }
}
