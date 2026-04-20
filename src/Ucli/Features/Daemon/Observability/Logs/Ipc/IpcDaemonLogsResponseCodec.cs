using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Decodes daemon-log IPC response envelopes into validated payload values. </summary>
internal static class IpcDaemonLogsResponseCodec
{
    /// <summary> Tries to decode one daemon-log IPC response. </summary>
    /// <param name="response"> The IPC response envelope. </param>
    /// <param name="payload"> The decoded daemon-log payload when decode succeeds. </param>
    /// <param name="error"> The structured decode error when decode fails. </param>
    /// <returns> <see langword="true" /> when decode succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryDecode (
        IpcResponse response,
        out IpcDaemonLogsReadResponse? payload,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
        {
            if (firstError is not null)
            {
                error = string.Equals(firstError.Code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal)
                    ? ExecutionError.InvalidArgument($"Daemon logs read failed with error code '{firstError.Code}'. {firstError.Message}")
                    : ExecutionError.InternalError($"Daemon logs read failed with error code '{firstError.Code}'. {firstError.Message}");
                payload = null;
                return false;
            }

            error = ExecutionError.InternalError($"Daemon logs read failed with status '{status}'.");
            payload = null;
            return false;
        }

        if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcDaemonLogsReadResponse parsedPayload, out var readError))
        {
            error = ExecutionError.InternalError($"Daemon logs read payload is invalid. {readError.Message}");
            payload = null;
            return false;
        }

        if (parsedPayload.Events is null)
        {
            error = ExecutionError.InternalError("Daemon logs read payload is invalid. Property 'events' must not be null.");
            payload = null;
            return false;
        }

        if (string.IsNullOrWhiteSpace(parsedPayload.NextCursor))
        {
            error = ExecutionError.InternalError("Daemon logs read payload is invalid. Property 'nextCursor' must not be empty.");
            payload = null;
            return false;
        }

        foreach (var daemonLogEvent in parsedPayload.Events)
        {
            if (daemonLogEvent is null
                || string.IsNullOrWhiteSpace(daemonLogEvent.Timestamp)
                || string.IsNullOrWhiteSpace(daemonLogEvent.Level)
                || string.IsNullOrWhiteSpace(daemonLogEvent.Category)
                || string.IsNullOrWhiteSpace(daemonLogEvent.Message)
                || string.IsNullOrWhiteSpace(daemonLogEvent.Cursor))
            {
                error = ExecutionError.InternalError("Daemon logs read payload is invalid. One or more event fields are missing.");
                payload = null;
                return false;
            }
        }

        payload = parsedPayload;
        error = null;
        return true;
    }
}