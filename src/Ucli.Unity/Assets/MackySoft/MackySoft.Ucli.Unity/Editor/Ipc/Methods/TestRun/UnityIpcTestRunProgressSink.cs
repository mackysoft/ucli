using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Serializes Unity test-run progress events onto one IPC stream frame writer. </summary>
    internal sealed class UnityIpcTestRunProgressSink : IUnityTestRunProgressSink
    {
        private readonly string runId;
        private readonly UnityIpcProgressFrameQueue progressFrameQueue;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcTestRunProgressSink" /> class. </summary>
        public UnityIpcTestRunProgressSink (
            IIpcStreamFrameWriter streamWriter,
            string runId,
            CancellationToken executionCancellationToken)
        {
            this.runId = string.IsNullOrWhiteSpace(runId) ? "unknown" : runId;
            progressFrameQueue = new UnityIpcProgressFrameQueue(
                streamWriter,
                executionCancellationToken,
                TestRunProgressEventNames.RunDiagnostic,
                CreateOverflowDiagnostic);
        }

        /// <inheritdoc />
        public void Publish (
            string eventName,
            object payload)
        {
            progressFrameQueue.Publish(eventName, payload);
        }

        /// <summary> Stops accepting progress events and waits for all previously accepted events to be delivered. </summary>
        /// <param name="cancellationToken"> The cancellation token propagated by the request. </param>
        /// <returns> A task that completes when progress acceptance has ended and accepted events are flushed. </returns>
        public Task CompleteAndFlushAsync (CancellationToken cancellationToken)
        {
            return progressFrameQueue.CompleteAndFlushAsync(cancellationToken);
        }

        private object CreateOverflowDiagnostic ()
        {
            return new TestRunDiagnosticEntry(
                runId,
                "TEST_PROGRESS_DROPPED",
                "Test progress entries exceeded the pending IPC frame limit; additional progress entries were dropped.",
                "warning");
        }
    }
}
