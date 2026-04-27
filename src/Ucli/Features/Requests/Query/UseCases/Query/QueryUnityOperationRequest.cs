using System.Text.Json;

namespace MackySoft.Ucli.Features.Requests.Query.UseCases.Query;

/// <summary> Represents one query operation that must execute inside Unity. </summary>
internal sealed record QueryUnityOperationRequest (
    string CommandName,
    string OperationId,
    string OperationName,
    JsonElement Args)
    : QueryOperationRequest(CommandName, OperationId, OperationName, Args);
