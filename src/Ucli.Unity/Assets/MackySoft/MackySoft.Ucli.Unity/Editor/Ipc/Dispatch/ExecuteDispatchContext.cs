namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents request-level context passed to execute request dispatching. </summary>
    /// <param name="RequestId"> The request identifier copied to response envelopes. </param>
    /// <param name="ProtocolVersion"> The protocol version copied to response envelopes. </param>
    internal sealed record ExecuteDispatchContext (
        string RequestId,
        int ProtocolVersion);
}
