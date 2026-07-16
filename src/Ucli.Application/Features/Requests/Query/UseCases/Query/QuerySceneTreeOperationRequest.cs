using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents one <c>query scene tree</c> operation request. </summary>
internal sealed record QuerySceneTreeOperationRequest (
    string CommandName,
    IpcExecuteStepId OperationId,
    string OperationName,
    UnityScenePath ScenePath,
    int? Depth,
    BoundedWindowOptions WindowOptions)
    : QueryOperationRequest(CommandName, OperationId, OperationName);
