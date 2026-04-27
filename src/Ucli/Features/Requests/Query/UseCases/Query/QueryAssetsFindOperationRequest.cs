namespace MackySoft.Ucli.Features.Requests.Query.UseCases.Query;

/// <summary> Represents one <c>query assets find</c> operation request. </summary>
internal sealed record QueryAssetsFindOperationRequest (
    string CommandName,
    string OperationId,
    string OperationName,
    QueryAssetsFindFilter Filter,
    QueryWindowOptions WindowOptions)
    : QueryOperationRequest(CommandName, OperationId, OperationName);
