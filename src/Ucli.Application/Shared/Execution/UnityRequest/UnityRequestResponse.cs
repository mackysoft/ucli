using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents one host-decoded Unity request response without exposing the IPC response envelope. </summary>
/// <param name="Payload"> The response payload body. </param>
/// <param name="Errors"> The machine-readable response errors. </param>
/// <param name="HasFailureStatus"> Whether the host observed a failed response status. </param>
internal sealed record UnityRequestResponse (
    JsonElement Payload,
    IReadOnlyList<OperationExecutionError> Errors,
    bool HasFailureStatus);
