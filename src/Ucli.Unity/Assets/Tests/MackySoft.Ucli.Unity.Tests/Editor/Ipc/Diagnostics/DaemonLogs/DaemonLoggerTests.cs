using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class DaemonLoggerTests
    {
        [Test]
        [Category("Size.Small")]
        public void Exception_WhenEmitted_DoesNotProduceUnprefixedRuntimeExceptionCopy ()
        {
            var daemonStream = new DaemonLogRingBuffer();
            var errorLogs = new List<string>();
            var daemonLogger = new DaemonLogger(
                daemonStream,
                new UnityMainThreadDaemonConsoleLogSink(
                    new SynchronizationContext(),
                    Thread.CurrentThread.ManagedThreadId,
                    _ => { },
                    _ => { },
                    errorLogs.Add));
            var exception = new InvalidOperationException("boom");

            daemonLogger.Exception("ipc", "daemon failed", exception);

            var snapshot = daemonStream.Snapshot();
            Assert.That(snapshot.Events.Count, Is.EqualTo(1));
            Assert.That(snapshot.Events[0].Message, Is.EqualTo("daemon failed"));
            Assert.That(snapshot.Events[0].Raw, Does.Contain("System.InvalidOperationException: boom"));
            Assert.That(errorLogs.Count, Is.EqualTo(1));
            Assert.That(errorLogs[0], Does.StartWith("[ucli][ipc] daemon failed"));
            Assert.That(errorLogs[0], Does.Not.StartWith("System.InvalidOperationException: boom"));
            Assert.That(errorLogs[0], Does.Contain("System.InvalidOperationException: boom"));
        }

        [Test]
        [Category("Size.Small")]
        public async Task ConsoleSink_WhenCalledFromWorkerThread_EmitsOnlyAfterMainThreadDispatch ()
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var synchronizationContext = new QueuedSynchronizationContext();
            var emittedThreadIds = new List<int>();
            var sink = new UnityMainThreadDaemonConsoleLogSink(
                synchronizationContext,
                mainThreadId,
                _ => emittedThreadIds.Add(Thread.CurrentThread.ManagedThreadId),
                _ => emittedThreadIds.Add(Thread.CurrentThread.ManagedThreadId),
                _ => emittedThreadIds.Add(Thread.CurrentThread.ManagedThreadId));

            await Task.Run(() => sink.Error("background failure"));

            Assert.That(emittedThreadIds, Is.Empty);
            Assert.That(synchronizationContext.PostedCount, Is.EqualTo(1));

            synchronizationContext.ExecutePostedCallbacks();

            Assert.That(emittedThreadIds, Is.EqualTo(new[] { mainThreadId }));
        }

        [Test]
        [Category("Size.Small")]
        public async Task CaptureCurrent_WhenWorkerHasSynchronizationContext_RejectsNonUnityThread ()
        {
            _ = UnityMainThreadDaemonConsoleLogSink.CaptureCurrent();

            InvalidOperationException exception = null;
            try
            {
                await Task.Run(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                    _ = UnityMainThreadDaemonConsoleLogSink.CaptureCurrent();
                });
            }
            catch (InvalidOperationException caughtException)
            {
                exception = caughtException;
            }

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.Message, Does.Contain("Unity Editor main thread"));
        }

        private sealed class QueuedSynchronizationContext : SynchronizationContext
        {
            private readonly Queue<(SendOrPostCallback Callback, object State)> callbacks =
                new Queue<(SendOrPostCallback Callback, object State)>();

            public int PostedCount => callbacks.Count;

            public override void Post (SendOrPostCallback callback, object state)
            {
                callbacks.Enqueue((callback, state));
            }

            public void ExecutePostedCallbacks ()
            {
                while (callbacks.Count > 0)
                {
                    var callback = callbacks.Dequeue();
                    callback.Callback(callback.State);
                }
            }
        }
    }
}
