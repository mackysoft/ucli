using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Execution;

/// <summary> Creates IPC request envelopes for Unity request execution clients. </summary>
internal static class UnityIpcRequestFactory
{
    /// <summary> Creates one request envelope with a generated request identifier. </summary>
    /// <param name="sessionToken"> The session token written into the request envelope. </param>
    /// <param name="method"> The IPC method name. </param>
    /// <param name="payload"> The payload element. </param>
    /// <returns> The created request envelope. </returns>
    public static IpcRequest Create (
        string sessionToken,
        string method,
        JsonElement payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"{method}-{Guid.NewGuid():N}",
            SessionToken: sessionToken,
            Method: method,
            Payload: payload);
    }
}
