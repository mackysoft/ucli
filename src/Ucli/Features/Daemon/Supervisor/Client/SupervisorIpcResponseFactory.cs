using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Client;

/// <summary> Creates supervisor IPC responses for successful and failed request handling. </summary>
internal static class SupervisorIpcResponseFactory
{
    private static readonly JsonElement EmptyPayload = IpcPayloadCodec.SerializeToElement(new { });

    /// <summary> Creates one successful supervisor response for the specified request and payload. </summary>
    /// <typeparam name="TPayload">The payload model type.</typeparam>
    /// <param name="request"> The incoming request. </param>
    /// <param name="payload"> The response payload. </param>
    /// <returns> The serialized success response. </returns>
    public static IpcResponse CreateSuccessResponse<TPayload> (
        IIpcRequestCorrelation request,
        TPayload payload)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: request.RequestId,
            status: IpcResponseStatus.Ok,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: Array.Empty<IpcError>());
    }

    /// <summary> Creates one failed supervisor response for the specified request. </summary>
    /// <param name="request"> The incoming request. </param>
    /// <param name="code"> The IPC error code. </param>
    /// <param name="message"> The IPC error message. </param>
    /// <param name="payload"> The structured response payload, or <see langword="null" /> to emit an empty object. </param>
    /// <returns> The serialized error response. </returns>
    public static IpcResponse CreateErrorResponse (
        IIpcRequestCorrelation request,
        UcliCode code,
        string message,
        JsonElement? payload = null)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: request.RequestId,
            status: IpcResponseStatus.Error,
            payload: payload ?? EmptyPayload,
            errors:
            [
                new IpcError(code, message, null),
            ]);
    }

    /// <summary> Creates one failed response for a malformed incoming IPC frame. </summary>
    /// <param name="errorKind"> The frame read-error kind. </param>
    /// <param name="errorMessage"> The frame read-error message. </param>
    /// <returns> The serialized malformed-frame response. </returns>
    public static IpcResponse CreateMalformedFrameResponse (
        IpcFrameReadErrorKind errorKind,
        string errorMessage)
    {
        var code = errorKind == IpcFrameReadErrorKind.PayloadTooLarge
            ? IpcProtocolErrorCodes.IpcFrameTooLarge
            : UcliCoreErrorCodes.InvalidArgument;
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: null,
            status: IpcResponseStatus.Error,
            payload: EmptyPayload,
            errors:
            [
                new IpcError(code, $"Supervisor IPC request frame is invalid. {errorMessage}", null),
            ]);
    }
}
