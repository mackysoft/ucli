using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one completed IPC connection exchange with optional request and response envelopes. </summary>
    internal readonly struct UnityIpcConnectionHandleResult
    {
        /// <summary> Initializes a new instance of the <see cref="UnityIpcConnectionHandleResult" /> struct. </summary>
        /// <param name="request"> The decoded request envelope when request decoding succeeded; otherwise <see langword="null" />. </param>
        /// <param name="response"> The response envelope written for the decoded request when available; otherwise <see langword="null" />. </param>
        /// <param name="isShutdownAdmissionCommitted"> Whether a successful shutdown response committed its mutation-admission seal. </param>
        public UnityIpcConnectionHandleResult (
            IpcRequest request,
            IpcResponse response,
            bool isShutdownAdmissionCommitted)
        {
            Request = request;
            Response = response;
            IsShutdownAdmissionCommitted = isShutdownAdmissionCommitted;
        }

        /// <summary> Gets the decoded request envelope when request decoding succeeded. </summary>
        internal IpcRequest Request { get; }

        /// <summary> Gets the response envelope written for the decoded request when available. </summary>
        internal IpcResponse Response { get; }

        /// <summary> Gets whether a successful shutdown response committed its mutation-admission seal. </summary>
        internal bool IsShutdownAdmissionCommitted { get; }
    }

    /// <summary> Defines the single terminal-response predicate that commits daemon shutdown. </summary>
    internal static class UnityIpcShutdownResponsePolicy
    {
        public static bool IsAccepted (
            IpcRequest request,
            IpcResponse response)
        {
            return request != null
                && response != null
                && string.Equals(request.Method, IpcMethodNames.Shutdown, System.StringComparison.Ordinal)
                && string.Equals(response.Status, IpcProtocol.StatusOk, System.StringComparison.Ordinal)
                && response.Errors.Count == 0;
        }
    }
}
