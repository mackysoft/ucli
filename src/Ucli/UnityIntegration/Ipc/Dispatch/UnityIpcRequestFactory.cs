using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

/// <summary> Creates IPC request envelopes for Unity request execution clients. </summary>
internal static class UnityIpcRequestFactory
{
    /// <summary> Creates an unauthenticated single-response ping used only to prove that the canonical endpoint is serving IPC. </summary>
    /// <param name="payload"> The ping payload element. </param>
    /// <param name="requestId"> The non-empty request identifier. </param>
    /// <param name="requestDeadlineUtc"> The UTC deadline shared by every delivery attempt for the logical request. </param>
    /// <param name="requestDeadlineRemainingMilliseconds"> The positive monotonic-clock time remaining until the shared deadline when this delivery attempt starts, rounded up to milliseconds. </param>
    /// <returns> The unauthenticated ping request envelope. </returns>
    public static IpcRequestEnvelope CreateUnauthenticatedPingProbe (
        JsonElement payload,
        Guid requestId,
        DateTimeOffset requestDeadlineUtc,
        int requestDeadlineRemainingMilliseconds)
    {
        return new IpcRequestEnvelope(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            sessionToken: string.Empty,
            method: ContractLiteralCodec.ToValue(UnityIpcMethod.Ping),
            payload: payload,
            responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
            requestDeadlineUtc: requestDeadlineUtc,
            requestDeadlineRemainingMilliseconds: requestDeadlineRemainingMilliseconds);
    }

    /// <summary> Creates one request envelope with the supplied request identifier. </summary>
    /// <param name="sessionToken"> The session token written into the request envelope. </param>
    /// <param name="method"> The defined Unity IPC method. </param>
    /// <param name="payload"> The payload element. </param>
    /// <param name="requestId"> The stable request identifier. </param>
    /// <param name="responseMode"> The response framing mode requested by the caller. </param>
    /// <param name="requestDeadlineUtc"> The UTC deadline shared by every delivery attempt for the logical request. </param>
    /// <param name="requestDeadlineRemainingMilliseconds"> The positive monotonic-clock time remaining until the shared deadline when this delivery attempt starts, rounded up to milliseconds. </param>
    /// <returns> The created request envelope. </returns>
    public static IpcRequestEnvelope Create (
        IpcSessionToken sessionToken,
        UnityIpcMethod method,
        JsonElement payload,
        Guid requestId,
        IpcResponseMode responseMode,
        DateTimeOffset requestDeadlineUtc,
        int requestDeadlineRemainingMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(sessionToken);
        return new IpcRequestEnvelope(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            sessionToken: sessionToken.GetEncodedValue(),
            method: ContractLiteralCodec.ToValue(method),
            payload: payload,
            responseMode: ContractLiteralCodec.ToValue(responseMode),
            requestDeadlineUtc: requestDeadlineUtc,
            requestDeadlineRemainingMilliseconds: requestDeadlineRemainingMilliseconds);
    }
}
