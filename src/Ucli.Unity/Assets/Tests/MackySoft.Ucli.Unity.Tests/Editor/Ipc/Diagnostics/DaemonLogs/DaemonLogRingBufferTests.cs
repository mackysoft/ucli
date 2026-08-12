using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class DaemonLogRingBufferTests
    {
        [Test]
        [Category("Size.Small")]
        public void Snapshot_WhenEventsAreWritten_ContainsMonotonicCursorSequence ()
        {
            var stream = new DaemonLogRingBuffer();
            stream.Write("ipc", IpcLogLevel.Info, "first");
            stream.Write("ipc", IpcLogLevel.Warning, "second");

            var snapshot = stream.Snapshot();

            Assert.That(snapshot.NextCursor.StreamId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(snapshot.Events.Count, Is.EqualTo(2));
            Assert.That(snapshot.Events[0].Cursor.StreamId, Is.EqualTo(snapshot.NextCursor.StreamId));
            Assert.That(snapshot.Events[1].Cursor.StreamId, Is.EqualTo(snapshot.NextCursor.StreamId));
            Assert.That(snapshot.Events[1].Cursor.Sequence, Is.EqualTo(snapshot.Events[0].Cursor.Sequence + 1));
            Assert.That(snapshot.NextCursor, Is.EqualTo(IpcLogCursor.Create(
                snapshot.NextCursor.StreamId,
                snapshot.Events[1].Cursor.Sequence + 1)));
        }

        [Test]
        [Category("Size.Small")]
        public void Snapshot_WhenCapacityIsExceeded_DropsOldestEvent ()
        {
            var stream = new DaemonLogRingBuffer();
            for (var i = 0; i < DaemonLogRingBuffer.Capacity + 1; i++)
            {
                stream.Write("ipc", IpcLogLevel.Info, $"event-{i}");
            }

            var snapshot = stream.Snapshot();

            Assert.That(snapshot.Events.Count, Is.EqualTo(DaemonLogRingBuffer.Capacity));
            Assert.That(snapshot.Events[0].Message, Is.EqualTo("event-1"));
            Assert.That(snapshot.Events[snapshot.Events.Count - 1].Message, Is.EqualTo($"event-{DaemonLogRingBuffer.Capacity}"));
        }

    }
}
