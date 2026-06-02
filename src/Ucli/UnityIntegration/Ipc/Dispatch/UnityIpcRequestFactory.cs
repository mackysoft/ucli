using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

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
    /// <param name="responseMode"> The response framing mode requested by the caller. </param>
    /// <returns> The created request envelope. </returns>
    public static IpcRequest Create (
        string sessionToken,
        string method,
        JsonElement payload,
        IpcResponseMode responseMode = IpcResponseMode.Single)
    {
        return Create(
            sessionToken,
            method,
            payload,
            CreateRequestId(method),
            responseMode);
    }

    /// <summary> Creates one request envelope from a dispatch request with a generated request identifier. </summary>
    /// <param name="sessionToken"> The session token written into the request envelope. </param>
    /// <param name="dispatchRequest"> The dispatch request that owns method, payload, and response mode. </param>
    /// <param name="dispatchTimeout"> The final dispatch timeout budget when the method needs server-side cancellation. </param>
    /// <returns> The created request envelope. </returns>
    public static IpcRequest Create (
        string sessionToken,
        UnityIpcDispatchRequest dispatchRequest,
        TimeSpan? dispatchTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        return Create(
            sessionToken,
            dispatchRequest,
            CreateRequestId(dispatchRequest.Method),
            dispatchTimeout);
    }

    /// <summary> Creates one request envelope with the supplied request identifier. </summary>
    /// <param name="sessionToken"> The session token written into the request envelope. </param>
    /// <param name="method"> The IPC method name. </param>
    /// <param name="payload"> The payload element. </param>
    /// <param name="requestId"> The stable request identifier. </param>
    /// <param name="responseMode"> The response framing mode requested by the caller. </param>
    /// <returns> The created request envelope. </returns>
    public static IpcRequest Create (
        string sessionToken,
        string method,
        JsonElement payload,
        string requestId,
        IpcResponseMode responseMode = IpcResponseMode.Single)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if (!ContractLiteralCodec.IsDefined(responseMode))
        {
            throw new ArgumentException($"Unsupported IPC response mode: {responseMode}.", nameof(responseMode));
        }

        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            SessionToken: sessionToken,
            Method: method,
            Payload: payload,
            ResponseMode: ContractLiteralCodec.ToValue(responseMode));
    }

    /// <summary> Creates one request envelope from a dispatch request with the supplied request identifier. </summary>
    /// <param name="sessionToken"> The session token written into the request envelope. </param>
    /// <param name="dispatchRequest"> The dispatch request that owns method, payload, and response mode. </param>
    /// <param name="requestId"> The stable request identifier. </param>
    /// <param name="dispatchTimeout"> The final dispatch timeout budget when the method needs server-side cancellation. </param>
    /// <returns> The created request envelope. </returns>
    public static IpcRequest Create (
        string sessionToken,
        UnityIpcDispatchRequest dispatchRequest,
        string requestId,
        TimeSpan? dispatchTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        return Create(
            sessionToken,
            dispatchRequest.Method,
            dispatchRequest.CreatePayload(dispatchTimeout),
            requestId,
            responseMode: dispatchRequest.ResponseMode);
    }
}
