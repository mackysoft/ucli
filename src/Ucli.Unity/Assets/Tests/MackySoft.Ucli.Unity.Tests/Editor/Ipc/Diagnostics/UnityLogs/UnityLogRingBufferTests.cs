using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityLogRingBufferTests
    {
        [Test]
        [Category("Size.Small")]
        public void Snapshot_WhenEventsWritten_ReturnsOrderedEventsAndNextCursor ()
        {
            var buffer = new UnityLogRingBuffer();

            buffer.Write(IpcUnityLogSource.Runtime, IpcLogLevel.Info, "runtime message");
            buffer.Write(IpcUnityLogSource.Compile, IpcLogLevel.Warning, "compile message");

            var snapshot = buffer.Snapshot();

            Assert.That(snapshot.NextCursor.StreamId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(snapshot.Events.Count, Is.EqualTo(2));
            Assert.That(snapshot.Events[0].Message, Is.EqualTo("runtime message"));
            Assert.That(snapshot.Events[1].Message, Is.EqualTo("compile message"));
            Assert.That(snapshot.Events[0].Cursor.StreamId, Is.EqualTo(snapshot.NextCursor.StreamId));
            Assert.That(snapshot.Events[0].Cursor.Sequence, Is.EqualTo(1));
            Assert.That(snapshot.NextCursor.Sequence, Is.EqualTo(3));
        }

        [Test]
        [Category("Size.Small")]
        public void Snapshot_WhenCapacityExceeded_DropsOldestEvents ()
        {
            var buffer = new UnityLogRingBuffer();

            for (var i = 0; i <= UnityLogRingBuffer.Capacity; i++)
            {
                buffer.Write(IpcUnityLogSource.Runtime, IpcLogLevel.Info, "message-" + i);
            }

            var snapshot = buffer.Snapshot();

            Assert.That(snapshot.Events.Count, Is.EqualTo(UnityLogRingBuffer.Capacity));
            Assert.That(snapshot.Events[0].Message, Is.EqualTo("message-1"));
            Assert.That(snapshot.Events[snapshot.Events.Count - 1].Message, Is.EqualTo("message-" + UnityLogRingBuffer.Capacity));
        }
    }
}
