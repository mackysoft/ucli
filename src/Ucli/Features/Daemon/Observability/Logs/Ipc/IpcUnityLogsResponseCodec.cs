using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Decodes Unity-log IPC response envelopes into validated payload values. </summary>
internal static class IpcUnityLogsResponseCodec
{
    /// <summary> Tries to decode one Unity-log IPC response. </summary>
    /// <param name="response"> The IPC response envelope. </param>
    /// <param name="payload"> The decoded Unity-log payload when decode succeeds. </param>
    /// <param name="error"> The structured decode error when decode fails. </param>
    /// <returns> <see langword="true" /> when decode succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryDecode (
        IpcResponse response,
        out IpcUnityLogsReadResponse? payload,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(response);

        return IpcLogsResponseDecodeHelper.TryDecodeReadPayload<IpcUnityLogsReadResponse, IpcUnityLogEvent>(
            response,
            "Unity logs read",
            static parsedPayload => parsedPayload.Events,
            static parsedPayload => parsedPayload.NextCursor,
            static unityLogEvent =>
                !string.IsNullOrWhiteSpace(unityLogEvent.Timestamp)
                && !string.IsNullOrWhiteSpace(unityLogEvent.Level)
                && !string.IsNullOrWhiteSpace(unityLogEvent.Source)
                && !string.IsNullOrWhiteSpace(unityLogEvent.Message)
                && !string.IsNullOrWhiteSpace(unityLogEvent.Cursor),
            out payload,
            out error);
    }
}
