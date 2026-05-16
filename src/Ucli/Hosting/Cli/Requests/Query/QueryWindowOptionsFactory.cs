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
        if (!BoundedWindowOptionsNormalizer.TryNormalize(
            all,
            limit,
            after,
            allConflictMessage: "'--all' cannot be combined with '--limit' or '--after'.",
            cursorErrorMessage: "after cursor is invalid.",
            out var options,
            out var errorMessage))
        {
            return QueryWindowOptionsCreationResult.Failure(ExecutionError.InvalidArgument(errorMessage));
        }

        return QueryWindowOptionsCreationResult.Success(options);
    }
}
