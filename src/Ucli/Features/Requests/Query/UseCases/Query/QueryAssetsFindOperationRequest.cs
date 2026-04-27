using System.Text.Json;
using MackySoft.Ucli.UnityIntegration.Indexing.Assets.Access;

namespace MackySoft.Ucli.Features.Requests.Query.UseCases.Query;

/// <summary> Represents one <c>query assets find</c> operation request. </summary>
internal sealed record QueryAssetsFindOperationRequest (
    string CommandName,
    string OperationId,
    string OperationName,
    JsonElement Args,
    AssetSearchLookupQuery Query,
    QueryWindowOptions WindowOptions)
    : QueryOperationRequest(CommandName, OperationId, OperationName, Args);
