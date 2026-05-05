using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Client;

/// <summary> Creates supervisor IPC responses for successful and failed request handling. </summary>
internal static class SupervisorIpcResponseFactory
{
    /// <summary> Creates one successful supervisor response for the specified request and payload. </summary>
    /// <typeparam name="TPayload">The payload model type.</typeparam>
    /// <param name="request"> The incoming request. </param>
    /// <param name="payload"> The response payload. </param>
    /// <returns> The serialized success response. </returns>
    public static IpcResponse CreateSuccessResponse<TPayload> (
        IpcRequest request,
        TPayload payload)
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: Array.Empty<IpcError>());
    }

    /// <summary> Creates one failed supervisor response for the specified request. </summary>
    /// <param name="request"> The incoming request. </param>
    /// <param name="code"> The IPC error code. </param>
    /// <param name="message"> The IPC error message. </param>
    /// <returns> The serialized error response. </returns>
    public static IpcResponse CreateErrorResponse (
        IpcRequest request,
        string code,
        string message)
    {
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(new { }),
            Errors:
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
            ? IpcErrorCodes.IpcFrameTooLarge
            : IpcErrorCodes.InvalidArgument;
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: string.Empty,
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(new { }),
            Errors:
            [
                new IpcError(code, $"Supervisor IPC request frame is invalid. {errorMessage}", null),
            ]);
    }
}
