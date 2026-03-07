using System;
using System.Collections.Generic;
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
            stream.Write("ipc", "info", "first");
            stream.Write("ipc", "warning", "second");

            var snapshot = stream.Snapshot();

            Assert.That(snapshot.Events.Count, Is.EqualTo(2));
            Assert.That(IpcLogCursorCodec.TryParse(snapshot.Events[0].Cursor, out var streamId, out var firstSequence), Is.True);
            Assert.That(IpcLogCursorCodec.TryParse(snapshot.Events[1].Cursor, out _, out var secondSequence), Is.True);
            Assert.That(streamId, Is.EqualTo(snapshot.StreamId));
            Assert.That(secondSequence, Is.EqualTo(firstSequence + 1));
            Assert.That(snapshot.NextCursor, Is.EqualTo(IpcLogCursorCodec.Encode(snapshot.StreamId, secondSequence + 1)));
        }

        [Test]
        [Category("Size.Small")]
        public void Snapshot_WhenCapacityIsExceeded_DropsOldestEvent ()
        {
            var stream = new DaemonLogRingBuffer();
            for (var i = 0; i < DaemonLogRingBuffer.Capacity + 1; i++)
            {
                stream.Write("ipc", "info", $"event-{i}");
            }

            var snapshot = stream.Snapshot();

            Assert.That(snapshot.Events.Count, Is.EqualTo(DaemonLogRingBuffer.Capacity));
            Assert.That(snapshot.Events[0].Message, Is.EqualTo("event-1"));
            Assert.That(snapshot.Events[snapshot.Events.Count - 1].Message, Is.EqualTo($"event-{DaemonLogRingBuffer.Capacity}"));
        }

        [Test]
        [Category("Size.Small")]
        public void Snapshot_WhenAfterCursorIsApplied_AllowsIncrementalFiltering ()
        {
            var stream = new DaemonLogRingBuffer();
            stream.Write("ipc", "info", "event-1");
            stream.Write("ipc", "warning", "event-2");
            stream.Write("transport", "warning", "event-3");
            var snapshot = stream.Snapshot();
            var afterCursor = snapshot.Events[1].Cursor;
            Assert.That(IpcLogCursorCodec.TryParse(afterCursor, out _, out var afterSequence), Is.True);

            var filtered = new List<DaemonLogEvent>();
            foreach (var daemonLogEvent in snapshot.Events)
            {
                if (daemonLogEvent.Sequence >= afterSequence
                    && string.Equals(daemonLogEvent.Level, "warning", StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(daemonLogEvent);
                }
            }

            Assert.That(filtered.Count, Is.EqualTo(2));
            Assert.That(filtered[0].Message, Is.EqualTo("event-2"));
            Assert.That(filtered[1].Message, Is.EqualTo("event-3"));
        }
    }
}
