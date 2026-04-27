namespace MackySoft.Ucli.Features.Requests.Query.UseCases.Query;

/// <summary> Represents one <c>query scene tree</c> operation request. </summary>
internal sealed record QuerySceneTreeOperationRequest (
    string CommandName,
    string OperationId,
    string OperationName,
    string ScenePath,
    int? Depth,
    QueryWindowOptions WindowOptions)
    : QueryOperationRequest(CommandName, OperationId, OperationName);
