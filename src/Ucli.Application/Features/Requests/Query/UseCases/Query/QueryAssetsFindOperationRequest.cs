using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents one <c>query assets find</c> operation request. </summary>
internal sealed record QueryAssetsFindOperationRequest (
    string CommandName,
    string OperationId,
    string OperationName,
    AssetSearchLookupQuery Query,
    BoundedWindowOptions WindowOptions)
    : QueryOperationRequest(CommandName, OperationId, OperationName);
