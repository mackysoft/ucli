using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Serializes Unity test-run progress events onto one IPC stream frame writer. </summary>
    internal sealed class UnityIpcTestRunProgressSink : IUnityTestRunProgressSink
    {
        private const int MaxPendingFrameCount = 1024;

        private readonly IUnityIpcStreamFrameWriter streamWriter;
        private readonly string runId;
        private readonly CancellationToken progressAcceptanceCancellationToken;
        private readonly CancellationToken frameWriteCancellationToken;

        private readonly object syncRoot = new object();

        private Task tail = Task.CompletedTask;
        private int pendingFrameCount;
        private bool overflowDiagnosticQueued;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcTestRunProgressSink" /> class. </summary>
        public UnityIpcTestRunProgressSink (
            IUnityIpcStreamFrameWriter streamWriter,
            string runId,
            CancellationToken progressAcceptanceCancellationToken,
            CancellationToken frameWriteCancellationToken)
        {
            this.streamWriter = streamWriter ?? throw new ArgumentNullException(nameof(streamWriter));
            this.runId = string.IsNullOrWhiteSpace(runId) ? "unknown" : runId;
            this.progressAcceptanceCancellationToken = progressAcceptanceCancellationToken;
            this.frameWriteCancellationToken = frameWriteCancellationToken;
        }

        /// <inheritdoc />
        public void Publish (
            string eventName,
            object payload)
        {
            progressAcceptanceCancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("Progress event name must not be empty.", nameof(eventName));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            lock (syncRoot)
            {
                if (pendingFrameCount >= MaxPendingFrameCount)
                {
                    if (overflowDiagnosticQueued)
                    {
                        return;
                    }

                    overflowDiagnosticQueued = true;
                    eventName = TestRunProgressEventNames.RunDiagnostic;
                    payload = new TestRunDiagnosticEntry(
                        runId,
                        "TEST_PROGRESS_DROPPED",
                        "Test progress entries exceeded the pending IPC frame limit; additional progress entries were dropped.",
                        "warning");
                }

                pendingFrameCount++;
                tail = WriteAfterPreviousAsync(tail, streamWriter, eventName, payload, ReleasePendingFrame, frameWriteCancellationToken);
            }
        }

        /// <inheritdoc />
        public async Task FlushAsync (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task pending;
            lock (syncRoot)
            {
                pending = tail;
            }

            await pending;
            cancellationToken.ThrowIfCancellationRequested();
        }

        private static async Task WriteAfterPreviousAsync (
            Task previous,
            IUnityIpcStreamFrameWriter streamWriter,
            string eventName,
            object payload,
            Action releasePendingFrame,
            CancellationToken cancellationToken)
        {
            try
            {
                await previous;
                cancellationToken.ThrowIfCancellationRequested();
                await streamWriter.WriteProgressAsync(eventName, payload, cancellationToken);
            }
            finally
            {
                releasePendingFrame();
            }
        }

        private void ReleasePendingFrame ()
        {
            lock (syncRoot)
            {
                pendingFrameCount--;
            }
        }
    }
}
