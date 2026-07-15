using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents either one validated Unity IPC request or its terminal validation error. </summary>
    internal sealed class UnityIpcRequestValidationResult
    {
        private UnityIpcRequestValidationResult (
            ValidatedUnityIpcRequest request,
            IpcResponse errorResponse,
            IpcResponseMode responseMode)
        {
            Request = request;
            ErrorResponse = errorResponse;
            ResponseMode = responseMode;
        }

        /// <summary> Gets whether validation produced a dispatchable request. </summary>
        public bool IsSuccess => Request != null;

        /// <summary> Gets the validated request, or <see langword="null" /> when validation failed. </summary>
        public ValidatedUnityIpcRequest Request { get; }

        /// <summary> Gets the terminal validation error, or <see langword="null" /> when validation succeeded. </summary>
        public IpcResponse ErrorResponse { get; }

        /// <summary> Gets the response framing mode selected while validating the envelope. </summary>
        public IpcResponseMode ResponseMode { get; }

        /// <summary> Creates a successful validation result. </summary>
        public static UnityIpcRequestValidationResult Success (ValidatedUnityIpcRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return new UnityIpcRequestValidationResult(request, null, request.ResponseMode);
        }

        /// <summary> Creates a failed validation result with the framing mode used to return the error. </summary>
        public static UnityIpcRequestValidationResult Failure (
            IpcResponse errorResponse,
            IpcResponseMode responseMode)
        {
            if (errorResponse == null)
            {
                throw new ArgumentNullException(nameof(errorResponse));
            }

            if (!ContractLiteralCodec.IsDefined(responseMode))
            {
                throw new ArgumentOutOfRangeException(nameof(responseMode), responseMode, "IPC response mode must be defined.");
            }

            return new UnityIpcRequestValidationResult(null, errorResponse, responseMode);
        }
    }
}
