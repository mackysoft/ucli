using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

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

        return IpcLogsResponseDecodeHelper.TryDecodeReadPayload<IpcDaemonLogsReadResponse, IpcDaemonLogEvent>(
            response,
            "Daemon logs read",
            static parsedPayload => parsedPayload.Events,
            static parsedPayload => parsedPayload.NextCursor,
            static daemonLogEvent =>
                !string.IsNullOrWhiteSpace(daemonLogEvent.Timestamp)
                && !string.IsNullOrWhiteSpace(daemonLogEvent.Level)
                && !string.IsNullOrWhiteSpace(daemonLogEvent.Category)
                && !string.IsNullOrWhiteSpace(daemonLogEvent.Message)
                && !string.IsNullOrWhiteSpace(daemonLogEvent.Cursor),
            out payload,
            out error);
    }
}
