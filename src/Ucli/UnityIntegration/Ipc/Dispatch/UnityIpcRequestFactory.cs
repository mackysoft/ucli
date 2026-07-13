using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

/// <summary> Creates IPC request envelopes for Unity request execution clients. </summary>
internal static class UnityIpcRequestFactory
{
    /// <summary> Creates one request envelope with the supplied request identifier. </summary>
    /// <param name="sessionToken"> The session token written into the request envelope. </param>
    /// <param name="method"> The defined Unity IPC method. </param>
    /// <param name="payload"> The payload element. </param>
    /// <param name="requestId"> The stable request identifier. </param>
    /// <param name="responseMode"> The response framing mode requested by the caller. </param>
    /// <returns> The created request envelope. </returns>
    public static IpcRequest Create (
        string sessionToken,
        UnityIpcMethod method,
        JsonElement payload,
        Guid requestId,
        IpcResponseMode responseMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        if (!ContractLiteralCodec.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Unity IPC method must be defined.");
        }

        if (!ContractLiteralCodec.IsDefined(responseMode))
        {
            throw new ArgumentException($"Unsupported IPC response mode: {responseMode}.", nameof(responseMode));
        }

        return new IpcRequest(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            sessionToken: sessionToken,
            method: ContractLiteralCodec.ToValue(method),
            payload: payload,
            responseMode: ContractLiteralCodec.ToValue(responseMode));
    }

}
