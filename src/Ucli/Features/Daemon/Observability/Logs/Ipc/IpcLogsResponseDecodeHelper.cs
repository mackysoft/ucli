using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Provides shared decoding for logs-related IPC response envelopes. </summary>
internal static class IpcLogsResponseDecodeHelper
{
    /// <summary> Tries to read a failure response from an IPC envelope. </summary>
    /// <param name="response"> The IPC response envelope. </param>
    /// <param name="operationName"> The operation name used in diagnostics. </param>
    /// <param name="error"> The mapped execution error when the response is a failure. </param>
    /// <returns> <see langword="true" /> when a failure response was decoded; otherwise <see langword="false" />. </returns>
    public static bool TryDecodeFailure (
        IpcResponse response,
        string operationName,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (!IpcResponseFailureReader.TryRead(response, out var firstError))
        {
            error = null;
            return false;
        }

        error = firstError.Code == UcliCoreErrorCodes.InvalidArgument
            ? ExecutionError.InvalidArgument($"{operationName} failed with error code '{firstError.Code}'. {firstError.Message}")
            : ExecutionError.InternalError($"{operationName} failed with error code '{firstError.Code}'. {firstError.Message}");
        return true;
    }

    /// <summary> Creates the common invalid payload decode error. </summary>
    /// <param name="operationName"> The operation name used in diagnostics. </param>
    /// <param name="message"> The payload validation message. </param>
    /// <returns> The invalid-payload execution error. </returns>
    public static ExecutionError CreateInvalidPayloadError (
        string operationName,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return ExecutionError.InternalError($"{operationName} payload is invalid. {message}");
    }

    /// <summary> Tries to decode and validate one logs read response payload. </summary>
    /// <typeparam name="TResponse"> The logs read response payload type. </typeparam>
    /// <param name="response"> The IPC response envelope. </param>
    /// <param name="operationName"> The operation name used in diagnostics. </param>
    /// <param name="payload"> The decoded payload when decode succeeds. </param>
    /// <param name="error"> The structured decode error when decode fails. </param>
    /// <returns> <see langword="true" /> when decode succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryDecodeReadPayload<TResponse> (
        IpcResponse response,
        string operationName,
        out TResponse? payload,
        out ExecutionError? error)
        where TResponse : class
    {
        if (TryDecodeFailure(response, operationName, out error))
        {
            payload = null;
            return false;
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out TResponse parsedPayload, out var readError))
        {
            error = CreateInvalidPayloadError(operationName, readError.Message);
            payload = null;
            return false;
        }

        payload = parsedPayload;
        error = null;
        return true;
    }
}
