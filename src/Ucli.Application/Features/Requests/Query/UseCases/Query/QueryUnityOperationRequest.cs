using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;

/// <summary> Represents one query operation that must execute inside Unity. </summary>
internal sealed record QueryUnityOperationRequest (
    string CommandName,
    IpcExecuteStepId OperationId,
    string OperationName,
    JsonElement Args)
    : QueryOperationRequest(CommandName, OperationId, OperationName);
