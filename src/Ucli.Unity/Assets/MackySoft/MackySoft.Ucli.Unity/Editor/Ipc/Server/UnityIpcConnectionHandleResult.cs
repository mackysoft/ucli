using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents whether one IPC connection wrote a correlated terminal response. </summary>
    internal sealed class UnityIpcConnectionHandleResult
    {
        /// <summary> Gets the result used when no correlated terminal response was written. </summary>
        public static UnityIpcConnectionHandleResult NoTerminalResponse { get; } = new UnityIpcConnectionHandleResult();

        /// <summary> Initializes a completed correlated connection exchange. </summary>
        /// <param name="request"> The authorized and validated Unity IPC request. </param>
        /// <param name="response"> The correlated terminal response written for <paramref name="request" />. </param>
        /// <param name="isShutdownAdmissionCommitted"> Whether a successful shutdown response committed its mutation-admission seal. </param>
        /// <exception cref="ArgumentNullException"> Thrown when an envelope is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when the response is not correlated or shutdown admission is inconsistent with the envelopes. </exception>
        public UnityIpcConnectionHandleResult (
            ValidatedUnityIpcRequest request,
            IpcResponse response,
            bool isShutdownAdmissionCommitted)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Response = response ?? throw new ArgumentNullException(nameof(response));

            if (response.RequestId != request.RequestId)
            {
                throw new ArgumentException(
                    "The terminal IPC response must be correlated to its request.",
                    nameof(response));
            }

            if (isShutdownAdmissionCommitted
                && !UnityIpcShutdownResponsePolicy.IsAccepted(request.Method, response))
            {
                throw new ArgumentException(
                    "Shutdown admission can be committed only for an accepted shutdown response.",
                    nameof(isShutdownAdmissionCommitted));
            }

            IsShutdownAdmissionCommitted = isShutdownAdmissionCommitted;
            HasTerminalResponse = true;
        }

        private UnityIpcConnectionHandleResult (IpcResponse validationErrorResponse)
        {
            Response = validationErrorResponse
                ?? throw new ArgumentNullException(nameof(validationErrorResponse));
            HasTerminalResponse = true;
        }

        private UnityIpcConnectionHandleResult ()
        {
        }

        /// <summary> Gets whether a correlated terminal response was written. </summary>
        internal bool HasTerminalResponse { get; }

        /// <summary> Gets the validated request, or <see langword="null" /> when validation failed or no terminal response was written. </summary>
        internal ValidatedUnityIpcRequest? Request { get; }

        /// <summary> Gets the validated Unity IPC method, or <see langword="null" /> when validation failed. </summary>
        internal UnityIpcMethod? Method => Request?.Method;

        /// <summary> Gets the correlated terminal response, or <see langword="null" /> when none was written. </summary>
        internal IpcResponse? Response { get; }

        /// <summary> Gets whether a successful shutdown response committed its mutation-admission seal. </summary>
        internal bool IsShutdownAdmissionCommitted { get; }

        /// <summary> Creates a completed exchange that returned a terminal request-validation error. </summary>
        public static UnityIpcConnectionHandleResult ValidationFailure (IpcResponse response)
        {
            return new UnityIpcConnectionHandleResult(response);
        }
    }

    /// <summary> Defines the single terminal-response predicate that commits daemon shutdown. </summary>
    internal static class UnityIpcShutdownResponsePolicy
    {
        public static bool IsAccepted (
            UnityIpcMethod? method,
            IpcResponse? response)
        {
            return method == UnityIpcMethod.Shutdown
                && response != null
                && response.Status == IpcResponseStatus.Ok;
        }
    }
}
