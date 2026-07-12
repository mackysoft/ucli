using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Dispatch
{
    /// <summary> Represents request-level context passed to execute request dispatching. </summary>
    internal sealed record ExecuteDispatchContext
    {
        /// <summary> Initializes request-level context passed to execute request dispatching. </summary>
        /// <param name="requestId"> The request identifier copied to response envelopes. </param>
        /// <param name="protocolVersion"> The protocol version copied to response envelopes. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty. </exception>
        public ExecuteDispatchContext (Guid requestId, int protocolVersion)
        {
            if (requestId == Guid.Empty)
            {
                throw new ArgumentException("Request id must not be empty.", nameof(requestId));
            }

            RequestId = requestId;
            ProtocolVersion = protocolVersion;
        }

        /// <summary> Gets the non-empty IPC request identifier copied to response envelopes. </summary>
        public Guid RequestId { get; }

        /// <summary> Gets the protocol version copied to response envelopes. </summary>
        public int ProtocolVersion { get; }

        /// <summary> Gets the Unity project identity served by this IPC host. </summary>
        public IpcProjectIdentity Project { get; init; } = IpcProjectIdentity.Unknown;
    }
}
