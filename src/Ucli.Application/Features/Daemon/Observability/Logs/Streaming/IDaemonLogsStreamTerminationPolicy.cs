namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;

/// <summary> Evaluates whether log-stream polling should stop after one batch. </summary>
internal interface IDaemonLogsStreamTerminationPolicy
{
    /// <summary> Gets the completion reason when stream loop should stop for current polling batch. </summary>
    /// <param name="events"> The current event batch. </param>
    /// <param name="now"> The current UTC timestamp. </param>
    /// <param name="untilTimestamp"> The optional inclusive upper timestamp bound. </param>
    /// <param name="lastEventTimestamp"> The last timestamp at which events were observed. </param>
    /// <param name="idleTimeout"> The optional idle-timeout threshold. </param>
    /// <param name="getTimestamp"> The accessor used to read event timestamps. </param>
    /// <returns> The completion reason when stream loop should stop; otherwise <see langword="null" />. </returns>
    string? GetStopReason<TEvent> (
        IReadOnlyList<TEvent> events,
        DateTimeOffset now,
        DateTimeOffset? untilTimestamp,
        DateTimeOffset lastEventTimestamp,
        TimeSpan? idleTimeout,
        Func<TEvent, string> getTimestamp);
}
