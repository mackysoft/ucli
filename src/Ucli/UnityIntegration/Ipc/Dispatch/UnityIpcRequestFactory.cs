using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

/// <summary> Creates IPC request envelopes for Unity request execution clients. </summary>
internal static class UnityIpcRequestFactory
{
    /// <summary> Creates one stable request identifier for one IPC method dispatch. </summary>
    /// <param name="method"> The IPC method name. </param>
    /// <returns> The created request identifier. </returns>
    public static string CreateRequestId (string method)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        return $"{method}-{Guid.NewGuid():N}";
    }

    /// <summary> Creates one request envelope with a generated request identifier. </summary>
    /// <param name="sessionToken"> The session token written into the request envelope. </param>
    /// <param name="method"> The IPC method name. </param>
    /// <param name="payload"> The payload element. </param>
    /// <param name="dispatchTimeout"> The final dispatch timeout budget when the method needs server-side cancellation. </param>
    /// <param name="responseMode"> The response framing mode requested by the caller. </param>
    /// <returns> The created request envelope. </returns>
    public static IpcRequest Create (
        string sessionToken,
        string method,
        JsonElement payload,
        TimeSpan? dispatchTimeout = null,
        string responseMode = IpcResponseModes.Single)
    {
        return Create(
            sessionToken,
            method,
            payload,
            CreateRequestId(method),
            dispatchTimeout,
            responseMode);
    }

    /// <summary> Creates one request envelope with the supplied request identifier. </summary>
    /// <param name="sessionToken"> The session token written into the request envelope. </param>
    /// <param name="method"> The IPC method name. </param>
    /// <param name="payload"> The payload element. </param>
    /// <param name="requestId"> The stable request identifier. </param>
    /// <param name="dispatchTimeout"> The final dispatch timeout budget when the method needs server-side cancellation. </param>
    /// <param name="responseMode"> The response framing mode requested by the caller. </param>
    /// <returns> The created request envelope. </returns>
    public static IpcRequest Create (
        string sessionToken,
        string method,
        JsonElement payload,
        string requestId,
        TimeSpan? dispatchTimeout = null,
        string responseMode = IpcResponseModes.Single)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if (!IpcResponseModes.IsDefined(responseMode))
        {
            throw new ArgumentException($"Unsupported IPC response mode: {responseMode}.", nameof(responseMode));
        }

        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            SessionToken: sessionToken,
            Method: method,
            Payload: ApplyDispatchTimeout(method, payload, dispatchTimeout),
            ResponseMode: responseMode);
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
