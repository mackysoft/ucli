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

        if (IpcResponseFailureReader.TryRead(response, out var firstError))
        {
            error = new DaemonPingResponseException(
                $"Daemon ping failed with error code '{firstError.Code}'.",
                firstError.Code);
            return false;
        }

        error = null;
        return true;
    }

    /// <summary> Tries to decode one ping payload from a successful response envelope. </summary>
    /// <param name="response"> The response returned from daemon. </param>
    /// <param name="payload"> The decoded ping payload when decoding succeeds; otherwise <see langword="null" />. </param>
    /// <param name="error"> The response decoding error when decoding fails; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the ping payload is decoded; otherwise <see langword="false" />. </returns>
    public static bool TryDecodePayload (
        IpcResponse response,
        out IpcUnityEditorObservation? payload,
        out DaemonPingResponseException? error)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!TryValidateSuccessResponse(response, out error))
        {
            payload = null;
            return false;
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcUnityEditorObservation parsedPayload, out var readError))
        {
            payload = null;
            error = new DaemonPingResponseException($"Daemon ping payload is invalid. {readError.Message}");
            return false;
        }

        payload = parsedPayload;
        error = null;
        return true;
    }

    /// <summary> Tries to decode one ping payload and validate that it belongs to the expected project. </summary>
    /// <param name="response"> The response returned from daemon. </param>
    /// <param name="expectedProjectFingerprint"> The expected project fingerprint. </param>
    /// <param name="operationName"> The operation name used in validation errors. </param>
    /// <param name="payload"> The decoded ping payload when decoding succeeds; otherwise <see langword="null" />. </param>
    /// <param name="error"> The response decoding error when decoding fails; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when ping payload is decoded and belongs to the expected project; otherwise <see langword="false" />. </returns>
    public static bool TryDecodePayloadForProject (
        IpcResponse response,
        ProjectFingerprint expectedProjectFingerprint,
        string operationName,
        out IpcUnityEditorObservation? payload,
        out DaemonPingResponseException? error)
    {
        ArgumentNullException.ThrowIfNull(expectedProjectFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (!TryDecodePayload(response, out payload, out error))
        {
            return false;
        }

        if (payload!.ProjectFingerprint != expectedProjectFingerprint)
        {
            error = new DaemonPingResponseException(
                $"{operationName} projectFingerprint mismatch. Requested={expectedProjectFingerprint}, Actual={payload.ProjectFingerprint}.");
            payload = null;
            return false;
        }

        return true;
    }
}
