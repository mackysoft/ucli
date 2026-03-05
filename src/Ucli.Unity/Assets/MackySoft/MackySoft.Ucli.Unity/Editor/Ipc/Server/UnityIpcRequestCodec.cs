using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Decodes IPC method payloads used by <see cref="UnityIpcMethodDispatcher" />. </summary>
    internal static class UnityIpcRequestCodec
    {
        /// <summary> Tries to decode one ping request payload. </summary>
        /// <param name="request"> The incoming request envelope. </param>
        /// <param name="payload"> The decoded payload when successful. </param>
        /// <param name="errorResponse"> The invalid-argument response when decoding fails. </param>
        /// <returns> <see langword="true" /> when decoding succeeded; otherwise <see langword="false" />. </returns>
        public static bool TryDecodePingRequest (
            IpcRequest request,
            out IpcPingRequest? payload,
            out IpcResponse? errorResponse)
        {
            return TryDecodePayload(request, "Ping", out payload, out errorResponse);
        }

        /// <summary> Tries to decode one execute request payload. </summary>
        /// <param name="request"> The incoming request envelope. </param>
        /// <param name="payload"> The decoded payload when successful. </param>
        /// <param name="errorResponse"> The invalid-argument response when decoding fails. </param>
        /// <returns> <see langword="true" /> when decoding succeeded; otherwise <see langword="false" />. </returns>
        public static bool TryDecodeExecuteRequest (
            IpcRequest request,
            out IpcExecuteRequest? payload,
            out IpcResponse? errorResponse)
        {
            return TryDecodePayload(request, "Execute", out payload, out errorResponse);
        }

        /// <summary> Tries to decode one test-run request payload. </summary>
        /// <param name="request"> The incoming request envelope. </param>
        /// <param name="payload"> The decoded payload when successful. </param>
        /// <param name="errorResponse"> The invalid-argument response when decoding fails. </param>
        /// <returns> <see langword="true" /> when decoding succeeded; otherwise <see langword="false" />. </returns>
        public static bool TryDecodeTestRunRequest (
            IpcRequest request,
            out IpcTestRunRequest? payload,
            out IpcResponse? errorResponse)
        {
            return TryDecodePayload(request, "TestRun", out payload, out errorResponse);
        }

        /// <summary> Tries to decode one shutdown request payload. </summary>
        /// <param name="request"> The incoming request envelope. </param>
        /// <param name="payload"> The decoded payload when successful. </param>
        /// <param name="errorResponse"> The invalid-argument response when decoding fails. </param>
        /// <returns> <see langword="true" /> when decoding succeeded; otherwise <see langword="false" />. </returns>
        public static bool TryDecodeShutdownRequest (
            IpcRequest request,
            out IpcShutdownRequest? payload,
            out IpcResponse? errorResponse)
        {
            return TryDecodePayload(request, "Shutdown", out payload, out errorResponse);
        }

        /// <summary> Tries to decode one method payload and creates standardized invalid-payload errors. </summary>
        /// <typeparam name="TPayload"> The payload model type. </typeparam>
        /// <param name="request"> The incoming request envelope. </param>
        /// <param name="methodName"> The IPC method name used in diagnostics. </param>
        /// <param name="payload"> The decoded payload when successful. </param>
        /// <param name="errorResponse"> The invalid-argument response when decoding fails. </param>
        /// <returns> <see langword="true" /> when decoding succeeded; otherwise <see langword="false" />. </returns>
        private static bool TryDecodePayload<TPayload> (
            IpcRequest request,
            string methodName,
            out TPayload? payload,
            out IpcResponse? errorResponse)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (IpcPayloadCodec.TryDeserialize(
                request.Payload,
                out TPayload parsedPayload,
                out var readError))
            {
                payload = parsedPayload;
                errorResponse = null;
                return true;
            }

            payload = default;
            var message = readError.Kind == IpcPayloadReadErrorKind.NullPayload
                ? $"{methodName} payload is null."
                : readError.Message;
            errorResponse = UnityIpcResponseFactory.CreateErrorResponse(
                request,
                IpcErrorCodes.InvalidArgument,
                $"{methodName} payload is invalid. {message}",
                null);
            return false;
        }
    }
}
