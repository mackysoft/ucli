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

            buffer.Write(IpcUnityLogsSourceCodec.Runtime, IpcDaemonLogsLevelCodec.Info, "runtime message");
            buffer.Write(IpcUnityLogsSourceCodec.Compile, IpcDaemonLogsLevelCodec.Warning, "compile message");

            var snapshot = buffer.Snapshot();

            Assert.That(snapshot.Events.Count, Is.EqualTo(2));
            Assert.That(snapshot.Events[0].Message, Is.EqualTo("runtime message"));
            Assert.That(snapshot.Events[1].Message, Is.EqualTo("compile message"));
            Assert.That(IpcLogCursorCodec.TryParse(snapshot.Events[0].Cursor, out var streamId, out var firstSequence), Is.True);
            Assert.That(firstSequence, Is.EqualTo(1));
            Assert.That(IpcLogCursorCodec.TryParse(snapshot.NextCursor, out var nextStreamId, out var nextSequence), Is.True);
            Assert.That(nextStreamId, Is.EqualTo(streamId));
            Assert.That(nextSequence, Is.EqualTo(3));
        }

        [Test]
        [Category("Size.Small")]
        public void Snapshot_WhenCapacityExceeded_DropsOldestEvents ()
        {
            var buffer = new UnityLogRingBuffer();

            for (var i = 0; i <= UnityLogRingBuffer.Capacity; i++)
            {
                buffer.Write(IpcUnityLogsSourceCodec.Runtime, IpcDaemonLogsLevelCodec.Info, "message-" + i);
            }

            var snapshot = buffer.Snapshot();

            Assert.That(snapshot.Events.Count, Is.EqualTo(UnityLogRingBuffer.Capacity));
            Assert.That(snapshot.Events[0].Message, Is.EqualTo("message-1"));
            Assert.That(snapshot.Events[snapshot.Events.Count - 1].Message, Is.EqualTo("message-" + UnityLogRingBuffer.Capacity));
        }
    }
}