using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Serializes bounded progress events onto one IPC stream frame writer. </summary>
    internal sealed class UnityIpcProgressFrameQueue
    {
        private const int MaxPendingFrameCount = 1024;

        private readonly IIpcStreamFrameWriter streamWriter;
        private readonly CancellationToken executionCancellationToken;
        private readonly string overflowEventName;
        private readonly Func<object> overflowDiagnosticFactory;

        private readonly object syncRoot = new object();

        private readonly Queue<QueuedProgressFrame> pendingFrames = new Queue<QueuedProgressFrame>();

        private Task drainTask = Task.CompletedTask;
        private int pendingFrameCount;
        private bool drainActive;
        private bool accepting = true;
        private bool overflowDiagnosticQueued;
        private Exception? drainFailure;

        /// <summary> Initializes a bounded progress frame queue. </summary>
        public UnityIpcProgressFrameQueue (
            IIpcStreamFrameWriter streamWriter,
            CancellationToken executionCancellationToken,
            string overflowEventName,
            Func<object> overflowDiagnosticFactory)
        {
            this.streamWriter = streamWriter ?? throw new ArgumentNullException(nameof(streamWriter));
            this.executionCancellationToken = executionCancellationToken;
            this.overflowEventName = !string.IsNullOrWhiteSpace(overflowEventName)
                ? overflowEventName
                : throw new ArgumentException("Overflow event name must not be empty.", nameof(overflowEventName));
            this.overflowDiagnosticFactory = overflowDiagnosticFactory ?? throw new ArgumentNullException(nameof(overflowDiagnosticFactory));
        }

        /// <summary> Queues one progress frame for IPC streaming. </summary>
        public void Publish (
            string eventName,
            object payload)
        {
            lock (syncRoot)
            {
                if (!accepting || executionCancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(eventName))
                {
                    throw new ArgumentException("Progress event name must not be empty.", nameof(eventName));
                }

                if (payload == null)
                {
                    throw new ArgumentNullException(nameof(payload));
                }

                if (drainFailure != null)
                {
                    // NOTE: Publish can run from synchronous Unity callbacks after the writer has faulted.
                    // Keep the first writer failure as the single error surfaced by CompleteAndFlushAsync.
                    return;
                }

                if (pendingFrameCount >= MaxPendingFrameCount)
                {
                    if (overflowDiagnosticQueued)
                    {
                        return;
                    }

                    overflowDiagnosticQueued = true;
                    eventName = overflowEventName;
                    payload = overflowDiagnosticFactory();
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

        /// <summary> Stops accepting progress frames and waits until all previously accepted frames have been written. </summary>
        public async Task CompleteAndFlushAsync (CancellationToken cancellationToken)
        {
            Task pending;
            lock (syncRoot)
            {
                accepting = false;
                pending = drainTask;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                ObserveFault(pending);
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (cancellationToken.CanBeCanceled)
            {
                var cancellationSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(static state =>
                {
                    var source = (TaskCompletionSource<bool>)state!;
                    source.TrySetResult(true);
                }, cancellationSource))
                {
                    var completedTask = await Task.WhenAny(pending, cancellationSource.Task).ConfigureAwait(false);
                    if (!ReferenceEquals(completedTask, pending))
                    {
                        ObserveFault(pending);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }

            await pending.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private static void ObserveFault (Task task)
        {
            _ = task.ContinueWith(
                static completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
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
                        executionCancellationToken.ThrowIfCancellationRequested();
                        await streamWriter.WriteProgressAsync(
                                frame.EventName,
                                frame.Payload,
                                executionCancellationToken)
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
