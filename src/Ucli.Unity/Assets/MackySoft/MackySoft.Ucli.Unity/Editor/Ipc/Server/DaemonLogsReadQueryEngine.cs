using System;
using System.Collections.Generic;
using System.Globalization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Applies normalized daemon-log read filters to stream snapshot events. </summary>
    internal sealed class DaemonLogsReadQueryEngine : IDaemonLogsReadQueryEngine
    {
        /// <inheritdoc />
        public IReadOnlyList<DaemonLogEvent> Filter (
            IReadOnlyList<DaemonLogEvent> events,
            DaemonLogsReadFilter filter)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            var filteredEvents = new List<DaemonLogEvent>(events.Count);
            var shouldApplySinceFilter = filter.Since.HasValue && !filter.AfterSequence.HasValue;
            foreach (var daemonLogEvent in events)
            {
                if (filter.AfterSequence.HasValue && daemonLogEvent.Sequence < filter.AfterSequence.Value)
                {
                    continue;
                }

                if (shouldApplySinceFilter
                    && TryParseEventTimestamp(daemonLogEvent.Timestamp, out var eventTimestampSince)
                    && eventTimestampSince < filter.Since!.Value)
                {
                    continue;
                }

                if (filter.Until.HasValue && TryParseEventTimestamp(daemonLogEvent.Timestamp, out var eventTimestampUntil) && eventTimestampUntil > filter.Until.Value)
                {
                    continue;
                }

                if (!string.Equals(filter.Level, IpcDaemonLogsLevelCodec.All, StringComparison.Ordinal)
                    && !string.Equals(daemonLogEvent.Level, filter.Level, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ShouldApplyCategoryFilter(filter.Category)
                    && !string.Equals(daemonLogEvent.Category, filter.Category, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(filter.Query)
                    && !MatchesQuery(daemonLogEvent, filter.Query, filter.QueryTarget))
                {
                    continue;
                }

                filteredEvents.Add(daemonLogEvent);
            }

            if (!filter.Tail.HasValue || filteredEvents.Count <= filter.Tail.Value)
            {
                return filteredEvents;
            }

            var tailEvents = new List<DaemonLogEvent>(filter.Tail.Value);
            var startIndex = filteredEvents.Count - filter.Tail.Value;
            for (var i = startIndex; i < filteredEvents.Count; i++)
            {
                tailEvents.Add(filteredEvents[i]);
            }

            return tailEvents;
        }

        /// <summary> Determines whether category filter should be applied. </summary>
        /// <param name="category"> The normalized category literal. </param>
        /// <returns> <see langword="true" /> when category-specific filtering should run; otherwise <see langword="false" />. </returns>
        private static bool ShouldApplyCategoryFilter (string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            return !IpcDaemonLogsCategoryCodec.IsAll(category);
        }

        /// <summary> Determines whether one daemon log event matches query filter. </summary>
        /// <param name="daemonLogEvent"> The daemon log event value. </param>
        /// <param name="query"> The normalized query value. </param>
        /// <param name="queryTarget"> The normalized query-target literal. </param>
        /// <returns> <see langword="true" /> when event matches query; otherwise <see langword="false" />. </returns>
        private static bool MatchesQuery (
            DaemonLogEvent daemonLogEvent,
            string query,
            string queryTarget)
        {
            var messageHit = daemonLogEvent.Message != null
                && daemonLogEvent.Message.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            if (string.Equals(queryTarget, IpcDaemonLogsQueryTargetCodec.Message, StringComparison.Ordinal))
            {
                return messageHit;
            }

            var rawHit = daemonLogEvent.Raw != null
                && daemonLogEvent.Raw.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            return messageHit || rawHit;
        }

        /// <summary> Tries to parse one daemon-log event timestamp string. </summary>
        /// <param name="timestampText"> The source timestamp text. </param>
        /// <param name="timestamp"> The parsed timestamp when successful. </param>
        /// <returns> <see langword="true" /> when parsing succeeded; otherwise <see langword="false" />. </returns>
        private static bool TryParseEventTimestamp (
            string timestampText,
            out DateTimeOffset timestamp)
        {
            return DateTimeOffset.TryParse(
                timestampText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out timestamp);
        }
    }
}
