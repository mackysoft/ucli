using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

/// <summary> Creates IPC request envelopes for Unity request execution clients. </summary>
internal static class UnityIpcRequestFactory
{
    /// <summary> Creates one request envelope with a generated request identifier. </summary>
    /// <param name="sessionToken"> The session token written into the request envelope. </param>
    /// <param name="method"> The IPC method name. </param>
    /// <param name="payload"> The payload element. </param>
    /// <param name="dispatchTimeout"> The final dispatch timeout budget when the method needs server-side cancellation. </param>
    /// <returns> The created request envelope. </returns>
    public static IpcRequest Create (
        string sessionToken,
        string method,
        JsonElement payload,
        TimeSpan? dispatchTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"{method}-{Guid.NewGuid():N}",
            SessionToken: sessionToken,
            Method: method,
            Payload: ApplyDispatchTimeout(method, payload, dispatchTimeout));
    }

    private static JsonElement ApplyDispatchTimeout (
        string method,
        JsonElement payload,
        TimeSpan? dispatchTimeout)
    {
        if (!dispatchTimeout.HasValue
            || !string.Equals(method, IpcMethodNames.Compile, StringComparison.Ordinal)
            || !IpcPayloadCodec.TryDeserialize(payload, out IpcCompileRequest compileRequest, out _))
        {
            return payload;
        }

        return IpcPayloadCodec.SerializeToElement(compileRequest with
        {
            TimeoutMilliseconds = checked((int)Math.Ceiling(dispatchTimeout.Value.TotalMilliseconds)),
        });
    }
}
