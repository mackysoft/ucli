using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Represents shutdown signal coordination for daemon bootstrap lifecycle. </summary>
    internal interface IDaemonShutdownSignal
    {
        /// <summary> Signals that daemon shutdown has been requested. </summary>
        void Signal ();

        /// <summary> Waits until daemon shutdown is requested. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> A task that completes when shutdown is requested. </returns>
        Task WaitAsync (CancellationToken cancellationToken = default);
    }
}