using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Encodes daemon-log query values into <c>daemon.logs.read</c> IPC request envelopes. </summary>
internal static class IpcDaemonLogsRequestCodec
{
    /// <summary> Creates one daemon-log read IPC request envelope. </summary>
    /// <param name="query"> The daemon-log query values. </param>
    /// <param name="sessionToken"> The daemon session token used for authorization. </param>
    /// <returns> The encoded IPC request envelope. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="query" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    public static IpcRequest CreateRequest (
        IpcDaemonLogsReadRequest query,
        string sessionToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        var payload = IpcPayloadCodec.SerializeToElement(query);
        return UnityIpcRequestFactory.Create(
            sessionToken,
            UnityIpcMethod.DaemonLogsRead,
            payload,
            Guid.NewGuid(),
            IpcResponseMode.Single);
    }
}
