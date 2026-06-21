using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Serializes Unity build-run progress events onto one IPC stream frame writer. </summary>
    internal sealed class UnityIpcBuildRunProgressSink
    {
        private const int MaxPendingFrameCount = 1024;

        private readonly IIpcStreamFrameWriter streamWriter;
        private readonly string runId;
        private readonly CancellationToken progressAcceptanceCancellationToken;
        private readonly CancellationToken frameWriteCancellationToken;

        private readonly object syncRoot = new object();

        private readonly Queue<QueuedProgressFrame> pendingFrames = new Queue<QueuedProgressFrame>();

        private Task drainTask = Task.CompletedTask;
        private int pendingFrameCount;
        private bool drainActive;
        private bool overflowDiagnosticQueued;
        private Exception? drainFailure;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcBuildRunProgressSink" /> class. </summary>
        public UnityIpcBuildRunProgressSink (
            IIpcStreamFrameWriter streamWriter,
            string runId,
            CancellationToken progressAcceptanceCancellationToken,
            CancellationToken frameWriteCancellationToken)
        {
            this.streamWriter = streamWriter ?? throw new ArgumentNullException(nameof(streamWriter));
            this.runId = string.IsNullOrWhiteSpace(runId) ? "unknown" : runId;
            this.progressAcceptanceCancellationToken = progressAcceptanceCancellationToken;
            this.frameWriteCancellationToken = frameWriteCancellationToken;
        }

        /// <summary> Queues one progress frame for IPC streaming. </summary>
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
                if (drainFailure != null)
                {
                    // NOTE: Publish can run from synchronous Unity callbacks after the writer has faulted.
                    // Keep the first writer failure as the single error surfaced by FlushAsync.
                    return;
                }

                if (pendingFrameCount >= MaxPendingFrameCount)
                {
                    if (overflowDiagnosticQueued)
                    {
                        return;
                    }

                    overflowDiagnosticQueued = true;
                    eventName = BuildRunProgressEventNames.Diagnostic;
                    payload = new BuildDiagnosticEntry(
                        runId,
                        "BUILD_PROGRESS_DROPPED",
                        IpcExecuteDiagnosticSeverityNames.Warning,
                        "Build progress entries exceeded the pending IPC frame limit; additional progress entries were dropped.",
                        BuildRunProgressPhaseNames.RunnerInvocation);
                }

                pendingFrames.Enqueue(new QueuedProgressFrame(eventName, payload));
                pendingFrameCount++;
                if (!drainActive)
                {
                    drainActive = true;
                    // NOTE: Keep one drain loop instead of chaining one continuation per frame; releasing a backlog
                    // must not recurse through UnitySynchronizationContext continuations.
                    drainTask = DrainAsync();
                }
            }
        }

        /// <summary> Waits until all accepted progress frames have been written. </summary>
        public async Task FlushAsync (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task pending;
            lock (syncRoot)
            {
                pending = drainTask;
            }

            await pending.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private async Task DrainAsync ()
        {
            try
            {
                while (true)
                {
                    QueuedProgressFrame frame;
                    lock (syncRoot)
                    {
                        if (pendingFrames.Count == 0)
                        {
                            drainActive = false;
                            return;
                        }

                        frame = pendingFrames.Dequeue();
                    }

                    try
                    {
                        frameWriteCancellationToken.ThrowIfCancellationRequested();
                        await streamWriter.WriteProgressAsync(
                                frame.EventName,
                                frame.Payload,
                                frameWriteCancellationToken)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        ReleasePendingFrame();
                    }
                }
            }
            catch (Exception exception)
            {
                ReleaseQueuedFramesAfterFailure(exception);
                throw;
            }
        }

        private void ReleasePendingFrame ()
        {
            lock (syncRoot)
            {
                pendingFrameCount--;
            }
        }

        private void ReleaseQueuedFramesAfterFailure (Exception exception)
        {
            lock (syncRoot)
            {
                drainFailure ??= exception;
                pendingFrameCount -= pendingFrames.Count;
                pendingFrames.Clear();
                drainActive = false;
            }
        }

        private readonly struct QueuedProgressFrame
        {
            public QueuedProgressFrame (
                string eventName,
                object payload)
            {
                EventName = eventName;
                Payload = payload;
            }

            public string EventName { get; }

            public object Payload { get; }
        }
    }
}
