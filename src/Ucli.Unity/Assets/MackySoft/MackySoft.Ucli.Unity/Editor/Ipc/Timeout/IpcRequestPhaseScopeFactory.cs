using System;
using System.Diagnostics;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Creates monotonic request phase scopes from UTC envelope deadlines. </summary>
    internal sealed class IpcRequestPhaseScopeFactory : IIpcRequestPhaseScopeFactory
    {
        /// <inheritdoc />
        public IpcRequestPhaseScope Create (
            IpcRequestEnvelope request,
            CancellationToken upstreamCancellationToken,
            TimeSpan maximumResponseFrameWriteDuration)
        {
            var elapsedTime = Stopwatch.StartNew();
            var plan = IpcRequestPhasePlan.Create(
                request,
                DateTimeOffset.UtcNow,
                maximumResponseFrameWriteDuration);
            return new IpcRequestPhaseScope(
                plan,
                elapsedTime,
                upstreamCancellationToken);
        }
    }
}
