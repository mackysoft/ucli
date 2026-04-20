using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Encodes Unity-log query values into <c>unity.logs.read</c> IPC request envelopes. </summary>
internal static class IpcUnityLogsRequestCodec
{
    /// <summary> Creates one Unity-log read IPC request envelope. </summary>
    /// <param name="query"> The Unity-log query values. </param>
    /// <param name="sessionToken"> The daemon session token used for authorization. </param>
    /// <returns> The encoded IPC request envelope. </returns>
    public static IpcRequest CreateRequest (
        IpcUnityLogsReadRequest query,
        string sessionToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        var payload = IpcPayloadCodec.SerializeToElement(query);
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"unity-logs-read-{Guid.NewGuid():N}",
            SessionToken: sessionToken,
            Method: IpcMethodNames.UnityLogsRead,
            Payload: payload);
    }
}