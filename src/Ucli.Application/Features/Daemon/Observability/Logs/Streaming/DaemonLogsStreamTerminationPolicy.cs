using System.Globalization;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;

/// <summary> Provides stop conditions for log-stream polling loops. </summary>
internal sealed class DaemonLogsStreamTerminationPolicy : IDaemonLogsStreamTerminationPolicy
{
    /// <inheritdoc />
    public bool ShouldStop<TEvent> (
        IReadOnlyList<TEvent> events,
        DateTimeOffset now,
        DateTimeOffset? untilTimestamp,
        DateTimeOffset lastEventTimestamp,
        TimeSpan? idleTimeout,
        Func<TEvent, string> getTimestamp)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(getTimestamp);
        return ShouldStopCore(
            events.Count,
            now,
            untilTimestamp,
            lastEventTimestamp,
            idleTimeout,
            index => getTimestamp(events[index]));
    }

    /// <summary> Determines whether stream loop should stop based on current runtime state. </summary>
    private static bool ShouldStopCore (
        int eventCount,
        DateTimeOffset now,
        DateTimeOffset? untilTimestamp,
        DateTimeOffset lastEventTimestamp,
        TimeSpan? idleTimeout,
        Func<int, string> getTimestamp)
    {
        if (untilTimestamp.HasValue && ShouldStopByUntil(untilTimestamp.Value, eventCount, now, getTimestamp))
        {
            return true;
        }

        if (idleTimeout.HasValue
            && eventCount == 0
            && now - lastEventTimestamp >= idleTimeout.Value)
        {
            return true;
        }

        return false;
    }

    private static bool ShouldStopByUntil (
        DateTimeOffset until,
        int eventCount,
        DateTimeOffset now,
        Func<int, string> getTimestamp)
    {
        if (eventCount == 0)
        {
            return now >= until;
        }

        for (var i = 0; i < eventCount; i++)
        {
            if (!DateTimeOffset.TryParse(
                    getTimestamp(i),
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
