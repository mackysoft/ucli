namespace MackySoft.Ucli.Logs;

/// <summary> Represents validated runtime options used by daemon-log stream orchestration. </summary>
/// <param name="PollInterval"> The validated polling interval. </param>
/// <param name="IdleTimeout"> The validated idle-timeout threshold when stream mode is enabled. </param>
/// <param name="UntilTimestamp"> The parsed inclusive upper timestamp bound. </param>
internal sealed record LogsDaemonValidatedRequest (
    TimeSpan PollInterval,
    TimeSpan? IdleTimeout,
    DateTimeOffset? UntilTimestamp);