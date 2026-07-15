using System;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Converts one request deadline into an exchange-owned monotonic phase scope. </summary>
    internal interface IIpcRequestPhaseScopeFactory
    {
        /// <summary> Creates the phase scope immediately after one request frame has been read. </summary>
        /// <param name="request"> The request envelope whose validated deadline defines the exchange cutoffs. </param>
        /// <param name="upstreamCancellationToken"> The method execution lifetime selected by connection policy. Recoverable methods receive host lifetime; other methods receive connection lifetime. </param>
        /// <param name="maximumResponseFrameWriteDuration"> The maximum duration reserved for the final response-write phase. </param>
        /// <returns> The created phase scope. </returns>
        IpcRequestPhaseScope Create (
            IpcRequestEnvelope request,
            CancellationToken upstreamCancellationToken,
            TimeSpan maximumResponseFrameWriteDuration);
    }
}
