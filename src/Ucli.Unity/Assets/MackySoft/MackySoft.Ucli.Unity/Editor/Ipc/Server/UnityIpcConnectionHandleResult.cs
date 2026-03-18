using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one completed IPC connection exchange with optional request and response envelopes. </summary>
    internal readonly struct UnityIpcConnectionHandleResult
    {
        /// <summary> Initializes a new instance of the <see cref="UnityIpcConnectionHandleResult" /> struct. </summary>
        /// <param name="request"> The decoded request envelope when request decoding succeeded; otherwise <see langword="null" />. </param>
        /// <param name="response"> The response envelope written for the decoded request when available; otherwise <see langword="null" />. </param>
        public UnityIpcConnectionHandleResult (
            IpcRequest request,
            IpcResponse response)
        {
            Request = request;
            Response = response;
        }

        /// <summary> Gets the decoded request envelope when request decoding succeeded. </summary>
        internal IpcRequest Request { get; }

        /// <summary> Gets the response envelope written for the decoded request when available. </summary>
        internal IpcResponse Response { get; }

        /// <summary> Gets a value indicating whether the connection exchange completed with one successful response. </summary>
        internal bool HasSuccessfulResponse =>
            Request != null
            && Response != null
            && string.Equals(Response.Status, IpcProtocol.StatusOk, StringComparison.Ordinal)
            && Response.Errors.Count == 0;
    }
}