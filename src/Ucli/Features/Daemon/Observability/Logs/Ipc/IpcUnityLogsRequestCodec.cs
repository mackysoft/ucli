using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

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
        IpcSessionToken sessionToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sessionToken);

        var payload = IpcPayloadCodec.SerializeToElement(query);
        return UnityIpcRequestFactory.Create(
            sessionToken,
            UnityIpcMethod.UnityLogsRead,
            payload,
            Guid.NewGuid(),
            IpcResponseMode.Single);
    }
}
