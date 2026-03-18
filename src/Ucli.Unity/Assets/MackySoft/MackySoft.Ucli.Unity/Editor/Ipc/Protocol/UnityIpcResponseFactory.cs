using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Builds Unity IPC success and error response envelopes. </summary>
    internal static class UnityIpcResponseFactory
    {
        /// <summary> Creates one successful response envelope. </summary>
        /// <typeparam name="TPayload"> The payload type. </typeparam>
        /// <param name="request"> The request envelope used as response context. </param>
        /// <param name="payload"> The response payload model. </param>
        /// <returns> The successful response envelope. </returns>
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

        /// <summary> Creates one error response envelope. </summary>
        /// <param name="request"> The request envelope used as response context. </param>
        /// <param name="code"> The machine-readable error code. </param>
        /// <param name="message"> The human-readable error message. </param>
        /// <param name="opId"> The related operation identifier when available. </param>
        /// <returns> The error response envelope. </returns>
        public static IpcResponse CreateErrorResponse (
            IpcRequest request,
            string code,
            string message,
            string opId)
        {
            return new IpcResponse(
                ProtocolVersion: request.ProtocolVersion,
                RequestId: request.RequestId,
                Status: IpcProtocol.StatusError,
                Payload: IpcPayloadCodec.SerializeToElement(new { }),
                Errors: new[]
                {
                    new IpcError(code, message, opId),
                });
        }

        /// <summary> Creates one malformed-frame response when request envelope cannot be deserialized. </summary>
        /// <param name="errorKind"> The machine-readable frame read error kind. </param>
        /// <param name="errorMessage"> The diagnostic frame read error message. </param>
        /// <returns> The malformed-frame response envelope. </returns>
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
                Errors: new[]
                {
                    new IpcError(code, $"IPC request frame is invalid. {errorMessage}", null),
                });
        }
    }
}