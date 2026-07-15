using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Applies normalized Unity-log read filters to stream snapshot events. </summary>
    internal sealed class UnityLogsReadQueryEngine
    {
        /// <summary> Filters one snapshot event set by normalized filter values. </summary>
        /// <param name="events"> The source event sequence. </param>
        /// <param name="filter"> The normalized filter. </param>
        /// <returns> The filtered event sequence. </returns>
        public IReadOnlyList<UnityLogsReadEvent> Filter (
            IReadOnlyList<UnityLogEvent> events,
            UnityLogsReadFilter filter)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            var filteredEvents = new List<UnityLogsReadEvent>(events.Count);
            foreach (var unityLogEvent in events)
            {
                if (!LogReadFilterUtilities.PassesSequenceAndTimeWindow(
                        unityLogEvent.Cursor.Sequence,
                        unityLogEvent.Timestamp,
                        filter.AfterSequence,
                        filter.Since,
                        filter.Until))
                {
                    continue;
                }

                if (filter.Level.HasValue && unityLogEvent.Level != filter.Level.Value)
                {
                    continue;
                }

                if (filter.Source.HasValue && unityLogEvent.Source != filter.Source.Value)
                {
                    continue;
                }

                var effectiveStackTrace = UnityLogsStackTraceFormatter.Format(
                    unityLogEvent.StackTrace,
                    unityLogEvent.Level,
                    filter.StackTraceMode,
                    filter.StackTraceMaxFrames,
                    filter.StackTraceMaxChars);
                if (!string.IsNullOrWhiteSpace(filter.Query)
                    && !LogReadFilterUtilities.MatchesQuery(
                        unityLogEvent.Message,
                        effectiveStackTrace,
                        filter.Query,
                        searchPrimaryText: filter.QueryTarget != IpcLogQueryTarget.Stack,
                        searchSecondaryText: filter.QueryTarget != IpcLogQueryTarget.Message))
                {
                    continue;
                }

                filteredEvents.Add(new UnityLogsReadEvent(
                    Timestamp: unityLogEvent.Timestamp,
                    Level: unityLogEvent.Level,
                    Source: unityLogEvent.Source,
                    Message: unityLogEvent.Message,
                    StackTrace: effectiveStackTrace,
                    Cursor: unityLogEvent.Cursor));
            }

            return LogReadFilterUtilities.ApplyTail(filteredEvents, filter.Tail);
        }
    }
}
