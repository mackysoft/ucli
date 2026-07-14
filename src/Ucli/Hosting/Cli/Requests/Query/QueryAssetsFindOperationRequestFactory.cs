using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

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
        if (!TryCreateQuery(type, pathPrefix, nameContains, out var query, out var error))
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
                Query: query!,
                WindowOptions: windowResult.Options!));
    }

    private static bool TryCreateQuery (
        string? type,
        string? pathPrefix,
        string? nameContains,
        out AssetSearchLookupQuery? query,
        out ExecutionError? error)
    {
        query = null;
        error = null;

        UnityTypeId? typeId = null;
        if (type is not null)
        {
            try
            {
                typeId = new UnityTypeId(type);
            }
            catch (ArgumentException exception)
            {
                error = ExecutionError.InvalidArgument($"Option '--type' is invalid. {exception.Message}");
                return false;
            }
        }

        UnityAssetPathPrefix? assetPathPrefix = null;
        if (pathPrefix is not null)
        {
            try
            {
                assetPathPrefix = new UnityAssetPathPrefix(pathPrefix);
            }
            catch (ArgumentException exception)
            {
                error = ExecutionError.InvalidArgument($"Option '--pathPrefix' is invalid. {exception.Message}");
                return false;
            }
        }

        try
        {
            query = new AssetSearchLookupQuery(typeId, assetPathPrefix, nameContains);
            return true;
        }
        catch (ArgumentException exception) when (exception.ParamName == "NameContains")
        {
            error = ExecutionError.InvalidArgument($"Option '--nameContains' is invalid. {exception.Message}");
            return false;
        }
        catch (ArgumentException)
        {
            error = ExecutionError.InvalidArgument(
                "query assets find requires at least one filter: --type, --pathPrefix, or --nameContains.");
            return false;
        }
    }
}
