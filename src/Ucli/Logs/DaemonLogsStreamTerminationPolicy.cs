using System.Globalization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Logs;

/// <summary> Provides stop conditions for daemon-log stream polling loop. </summary>
internal sealed class DaemonLogsStreamTerminationPolicy : IDaemonLogsStreamTerminationPolicy
{
    /// <inheritdoc />
    public bool ShouldStop (
        IReadOnlyList<IpcDaemonLogEvent> events,
        DateTimeOffset now,
        DateTimeOffset? untilTimestamp,
        DateTimeOffset lastEventTimestamp,
        TimeSpan? idleTimeout)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (untilTimestamp.HasValue && ShouldStopByUntil(untilTimestamp.Value, events, now))
        {
            return true;
        }

        if (idleTimeout.HasValue
            && events.Count == 0
            && now - lastEventTimestamp >= idleTimeout.Value)
        {
            return true;
        }

        return false;
    }

    /// <summary> Determines whether stream loop should stop based on <c>until</c> constraint. </summary>
    /// <param name="until"> The inclusive upper timestamp bound. </param>
    /// <param name="events"> The current batch events. </param>
    /// <param name="now"> The current UTC timestamp. </param>
    /// <returns> <see langword="true" /> when stream loop should stop; otherwise <see langword="false" />. </returns>
    private static bool ShouldStopByUntil (
        DateTimeOffset until,
        IReadOnlyList<IpcDaemonLogEvent> events,
        DateTimeOffset now)
    {
        if (events.Count == 0)
        {
            return now >= until;
        }

        foreach (var daemonLogEvent in events)
        {
            if (!DateTimeOffset.TryParse(
                    daemonLogEvent.Timestamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var eventTimestamp))
            {
                continue;
            }

            if (eventTimestamp >= until)
            {
                return true;
            }
        }

        return false;
    }
}