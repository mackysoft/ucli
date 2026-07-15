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

                if (filter.Level.HasValue && daemonLogEvent.Level != filter.Level.Value)
                {
                    continue;
                }

                if (filter.Category != null
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
                        searchSecondaryText: filter.QueryTarget == IpcLogQueryTarget.Both))
                {
                    continue;
                }

                filteredEvents.Add(daemonLogEvent);
            }

            return LogReadFilterUtilities.ApplyTail(filteredEvents, filter.Tail);
        }
    }
}
