using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

namespace MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;

/// <summary> Encodes Unity Editor Console clear values into <c>unity.console.clear</c> IPC request envelopes. </summary>
internal static class IpcUnityConsoleClearRequestCodec
{
    /// <summary> Creates one Unity Editor Console clear IPC request envelope. </summary>
    /// <param name="sessionToken"> The daemon session token used for authorization. </param>
    /// <returns> The encoded IPC request envelope. </returns>
    public static IpcRequest CreateRequest (string sessionToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        var payload = IpcPayloadCodec.SerializeToElement(new IpcUnityConsoleClearRequest(UcliCommandIds.LogsUnityClear.Name));
        return UnityIpcRequestFactory.Create(
            sessionToken,
            UnityIpcMethod.UnityConsoleClear,
            payload,
            Guid.NewGuid(),
            IpcResponseMode.Single);
    }
}
