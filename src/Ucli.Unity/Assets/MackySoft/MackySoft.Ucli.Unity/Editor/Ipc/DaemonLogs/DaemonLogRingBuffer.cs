using System;
using System.Collections.Generic;
using System.Globalization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Provides in-memory ring-buffer storage for daemon control-log events. </summary>
    internal sealed class DaemonLogRingBuffer : IDaemonLogStream
    {
        /// <summary> Gets the maximum number of events retained in memory. </summary>
        public const int Capacity = 10000;

        private readonly object syncRoot = new object();

        private readonly DaemonLogEvent[] events = new DaemonLogEvent[Capacity];

        private readonly string streamId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        private int startIndex;

        private int count;

        private long nextSequence = 1;

        /// <inheritdoc />
        public void Write (
            string category,
            string level,
            string message,
            string raw = null)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                throw new ArgumentException("category must not be empty.", nameof(category));
            }

            if (string.IsNullOrWhiteSpace(level))
            {
                throw new ArgumentException("level must not be empty.", nameof(level));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("message must not be empty.", nameof(message));
            }

            lock (syncRoot)
            {
                var sequence = nextSequence++;
                var cursor = IpcLogCursorCodec.Encode(streamId, sequence);
                var daemonLogEvent = new DaemonLogEvent(
                    Sequence: sequence,
                    Timestamp: DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    Level: level,
                    Category: category,
                    Message: message,
                    Raw: raw,
                    Cursor: cursor);
                if (count < Capacity)
                {
                    var appendIndex = (startIndex + count) % Capacity;
                    events[appendIndex] = daemonLogEvent;
                    count++;
                    return;
                }

                events[startIndex] = daemonLogEvent;
                startIndex = (startIndex + 1) % Capacity;
            }
        }

        /// <inheritdoc />
        public DaemonLogSnapshot Snapshot ()
        {
            lock (syncRoot)
            {
                var snapshotEvents = new List<DaemonLogEvent>(count);
                for (var i = 0; i < count; i++)
                {
                    var index = (startIndex + i) % Capacity;
                    var daemonLogEvent = events[index];
                    if (daemonLogEvent != null)
                    {
                        snapshotEvents.Add(daemonLogEvent);
                    }
                }

                return new DaemonLogSnapshot(
                    StreamId: streamId,
                    NextCursor: IpcLogCursorCodec.Encode(streamId, nextSequence),
                    Events: snapshotEvents);
            }
        }
    }
}
