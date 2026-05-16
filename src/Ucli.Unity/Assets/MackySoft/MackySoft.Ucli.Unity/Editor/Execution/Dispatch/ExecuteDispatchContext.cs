using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Dispatch
{
    /// <summary> Represents request-level context passed to execute request dispatching. </summary>
    /// <param name="RequestId"> The request identifier copied to response envelopes. </param>
    /// <param name="ProtocolVersion"> The protocol version copied to response envelopes. </param>
    internal sealed record ExecuteDispatchContext (
        string RequestId,
        int ProtocolVersion)
    {
        /// <summary> Gets the Unity project identity served by this IPC host. </summary>
        public IpcProjectIdentity Project { get; init; } = IpcProjectIdentity.Unknown;
    }
}
