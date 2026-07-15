using System.Globalization;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates normalized query result windowing options from CLI values. </summary>
internal static class QueryWindowOptionsFactory
{
    /// <summary> Attempts to create normalized window options. </summary>
    public static QueryWindowOptionsCreationResult Create (
        bool all,
        int? limit,
        string? after)
    {
        if (!BoundedWindowOptions.TryCreate(
            all,
            limit,
            after,
            out var options,
            out var failure))
        {
            var errorMessage = failure switch
            {
                BoundedWindowOptionsCreationFailure.AllConflict => "'--all' cannot be combined with '--limit' or '--after'.",
                BoundedWindowOptionsCreationFailure.LimitOutOfRange => string.Format(
                    CultureInfo.InvariantCulture,
                    "limit must be between 1 and {0}. Actual: {1}.",
                    BoundedWindowConstants.MaxLimit,
                    limit),
                BoundedWindowOptionsCreationFailure.InvalidCursor => "after cursor is invalid.",
                _ => throw new InvalidOperationException($"Unsupported bounded window creation failure: {failure}."),
            };
            return QueryWindowOptionsCreationResult.Failure(ExecutionError.InvalidArgument(errorMessage));
        }

        return QueryWindowOptionsCreationResult.Success(options);
    }
}
