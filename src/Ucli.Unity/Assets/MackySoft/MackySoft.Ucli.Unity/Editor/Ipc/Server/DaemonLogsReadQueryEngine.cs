using System;
using System.Collections.Generic;
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
            foreach (var daemonLogEvent in events)
            {
                if (!LogReadFilterUtilities.PassesSequenceAndTimeWindow(
                        daemonLogEvent.Sequence,
                        daemonLogEvent.Timestamp,
                        filter.AfterSequence,
                        filter.Since,
                        filter.Until))
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
                    && !LogReadFilterUtilities.MatchesQuery(
                        daemonLogEvent.Message,
                        daemonLogEvent.Raw,
                        filter.Query,
                        searchPrimaryText: true,
                        searchSecondaryText: string.Equals(filter.QueryTarget, IpcDaemonLogsQueryTargetCodec.Both, StringComparison.Ordinal)))
                {
                    continue;
                }

                filteredEvents.Add(daemonLogEvent);
            }

            return LogReadFilterUtilities.ApplyTail(filteredEvents, filter.Tail);
        }

        private static bool ShouldApplyCategoryFilter (string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            return !IpcDaemonLogsCategoryCodec.IsAll(category);
        }
    }
}