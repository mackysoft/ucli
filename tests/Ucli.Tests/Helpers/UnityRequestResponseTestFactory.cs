using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class UnityRequestResponseTestFactory
{
    public static UnityRequestResponse Create (IpcResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var errors = new OperationExecutionError[response.Errors.Count];
        for (var i = 0; i < response.Errors.Count; i++)
        {
            var error = response.Errors[i];
            errors[i] = new OperationExecutionError(error.Code, error.Message, error.OpId);
        }

        return new UnityRequestResponse(
            Payload: response.Payload,
            Errors: errors,
            HasFailureStatus: !string.Equals(response.Status, IpcProtocol.StatusOk, StringComparison.Ordinal));
    }
}
