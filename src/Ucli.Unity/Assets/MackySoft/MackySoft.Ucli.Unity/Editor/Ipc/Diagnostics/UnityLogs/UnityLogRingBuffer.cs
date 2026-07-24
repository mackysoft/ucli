using System;
using System.Collections.Generic;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Provides in-memory ring-buffer storage for Unity log events. </summary>
    internal sealed class UnityLogRingBuffer : IUnityLogStream
    {
        /// <summary> Gets the maximum number of events retained in memory. </summary>
        public const int Capacity = 10000;

        private readonly object syncRoot = new object();

        private readonly UnityLogEvent[] events = new UnityLogEvent[Capacity];

        private readonly Guid streamId = Guid.NewGuid();

        private int startIndex;

        private int count;

        private long nextSequence = 1;

        /// <inheritdoc />
        public void Write (
            IpcUnityLogSource source,
            IpcLogLevel level,
            string message,
            string stackTrace = null)
        {
            if (!TextVocabulary.IsDefined(source))
            {
                throw new ArgumentOutOfRangeException(nameof(source), source, "Unity log source must be defined.");
            }

            if (!TextVocabulary.IsDefined(level))
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Unity log level must be defined.");
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("message must not be empty.", nameof(message));
            }

            lock (syncRoot)
            {
                var sequence = nextSequence++;
                var cursor = IpcLogCursor.Create(streamId, sequence);
                var unityLogEvent = new UnityLogEvent(
                    Timestamp: DateTimeOffset.UtcNow,
                    Level: level,
                    Source: source,
                    Message: message,
                    StackTrace: stackTrace,
                    Cursor: cursor);
                if (count < Capacity)
                {
                    var appendIndex = (startIndex + count) % Capacity;
                    events[appendIndex] = unityLogEvent;
                    count++;
                    return;
                }

                events[startIndex] = unityLogEvent;
                startIndex = (startIndex + 1) % Capacity;
            }
        }

        /// <inheritdoc />
        public UnityLogSnapshot Snapshot ()
        {
            lock (syncRoot)
            {
                var snapshotEvents = new List<UnityLogEvent>(count);
                for (var i = 0; i < count; i++)
                {
                    var index = (startIndex + i) % Capacity;
                    var unityLogEvent = events[index];
                    if (unityLogEvent != null)
                    {
                        snapshotEvents.Add(unityLogEvent);
                    }
                }

                return new UnityLogSnapshot(
                    NextCursor: IpcLogCursor.Create(streamId, nextSequence),
                    Events: snapshotEvents);
            }
        }
    }
}
