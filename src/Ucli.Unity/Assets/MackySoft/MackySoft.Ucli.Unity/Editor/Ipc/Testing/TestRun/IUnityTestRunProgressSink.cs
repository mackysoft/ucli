using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Receives Unity Test Framework progress events from synchronous callbacks. </summary>
    internal interface IUnityTestRunProgressSink
    {
        /// <summary> Queues one progress event for delivery. </summary>
        /// <param name="eventName"> The stream event name. </param>
        /// <param name="payload"> The stream event payload. </param>
        void Publish (
            string eventName,
            object payload);

        /// <summary> Waits until all queued progress events are delivered. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by the request. </param>
        /// <returns> A task that completes when queued events are flushed. </returns>
        Task FlushAsync (CancellationToken cancellationToken = default);
    }
}
