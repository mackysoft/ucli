using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Validates and decodes daemon ping responses for probe workflows. </summary>
internal static class DaemonPingResponseCodec
{
    /// <summary> Tries to validate one ping response as protocol-level success. </summary>
    /// <param name="response"> The response returned from daemon. </param>
    /// <param name="error"> The protocol error when validation fails; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when response status and error entries indicate success; otherwise <see langword="false" />. </returns>
    public static bool TryValidateSuccessResponse (
        IpcResponse response,
        out DaemonPingResponseException? error)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
        {
            if (firstError is not null)
            {
                error = new DaemonPingResponseException(
                    $"Daemon ping failed with error code '{firstError.Code}'.",
                    firstError.Code);
                return false;
            }

            error = new DaemonPingResponseException($"Daemon ping failed with status '{status}'.");
            return false;
        }

        error = null;
        return true;
    }

    /// <summary> Tries to decode one ping payload from a successful response envelope. </summary>
    /// <param name="response"> The response returned from daemon. </param>
    /// <param name="payload"> The decoded ping payload when decoding succeeds; otherwise <see langword="null" />. </param>
    /// <param name="error"> The response decoding error when decoding fails; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when ping payload is decoded and required fields are non-empty; otherwise <see langword="false" />. </returns>
    public static bool TryDecodePayload (
        IpcResponse response,
        out IpcPingResponse? payload,
        out DaemonPingResponseException? error)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!TryValidateSuccessResponse(response, out error))
        {
            payload = null;
            return false;
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcPingResponse parsedPayload, out var readError))
        {
            payload = null;
            error = new DaemonPingResponseException($"Daemon ping payload is invalid. {readError.Message}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(parsedPayload.ServerVersion)
            || string.IsNullOrWhiteSpace(parsedPayload.Runtime)
            || string.IsNullOrWhiteSpace(parsedPayload.UnityVersion))
        {
            payload = null;
            error = new DaemonPingResponseException("Daemon ping payload is invalid. One or more required fields are empty.");
            return false;
        }

        payload = parsedPayload;
        error = null;
        return true;
    }
}
