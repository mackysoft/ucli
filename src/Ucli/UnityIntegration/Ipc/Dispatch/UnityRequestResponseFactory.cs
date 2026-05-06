using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

/// <summary> Converts Unity IPC response envelopes into application-level request responses. </summary>
internal static class UnityRequestResponseFactory
{
    /// <summary> Creates one host-decoded Unity request response. </summary>
    /// <param name="response"> The raw Unity IPC response. </param>
    /// <returns> The application-level response. </returns>
    public static UnityRequestResponse Create (IpcResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var errors = new OperationExecutionError[response.Errors.Count];
        for (var i = 0; i < response.Errors.Count; i++)
        {
            var error = response.Errors[i];
            errors[i] = new OperationExecutionError(error.Code, error.Message, error.OpId);
        }

        var hasFailureStatus = !string.Equals(response.Status, IpcProtocol.StatusOk, StringComparison.Ordinal);
        return new UnityRequestResponse(
            Payload: response.Payload,
            Errors: errors,
            HasFailureStatus: hasFailureStatus,
            FailureStatus: hasFailureStatus ? response.Status : null);
    }
}
