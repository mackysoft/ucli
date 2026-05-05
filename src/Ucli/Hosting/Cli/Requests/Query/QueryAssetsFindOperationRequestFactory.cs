using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates normalized <c>assets.find</c> operation requests from raw command option values. </summary>
internal static class QueryAssetsFindOperationRequestFactory
{
    /// <summary> Attempts to create one normalized <c>assets.find</c> operation request. </summary>
    public static QueryAssetsFindOperationRequestCreationResult Create (
        string commandName,
        string operationId,
        string operationName,
        string? type,
        string? pathPrefix,
        string? nameContains,
        bool all,
        int? limit,
        string? after)
    {
        if (!TryCreateFilter(type, pathPrefix, nameContains, out var filter, out var error))
        {
            return QueryAssetsFindOperationRequestCreationResult.Failure(error!);
        }

        var windowResult = QueryWindowOptionsFactory.Create(all, limit, after);
        if (!windowResult.IsSuccess)
        {
            return QueryAssetsFindOperationRequestCreationResult.Failure(windowResult.Error!);
        }

        return QueryAssetsFindOperationRequestCreationResult.Success(
            new QueryAssetsFindOperationRequest(
                CommandName: commandName,
                OperationId: operationId,
                OperationName: operationName,
                Filter: filter!,
                WindowOptions: windowResult.Options!));
    }

    private static bool TryCreateFilter (
        string? type,
        string? pathPrefix,
        string? nameContains,
        out QueryAssetsFindFilter? filter,
        out ExecutionError? error)
    {
        filter = null;
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(type, "type", out var normalizedType, out error))
        {
            return false;
        }
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(pathPrefix, "pathPrefix", out var normalizedPathPrefix, out error))
        {
            return false;
        }
        if (!QueryOptionValueNormalizer.TryNormalizeOptional(nameContains, "nameContains", out var normalizedNameContains, out error))
        {
            return false;
        }

        if (normalizedType is null
            && normalizedPathPrefix is null
            && normalizedNameContains is null)
        {
            error = ExecutionError.InvalidArgument(
                "query assets find requires at least one filter: --type, --pathPrefix, or --nameContains.");
            return false;
        }

        filter = new QueryAssetsFindFilter(
            TypeId: normalizedType,
            PathPrefix: normalizedPathPrefix,
            NameContains: normalizedNameContains);
        return true;
    }
}
