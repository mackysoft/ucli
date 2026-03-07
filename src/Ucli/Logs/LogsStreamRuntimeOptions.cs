namespace MackySoft.Ucli.Logs;

/// <summary> Represents validated runtime options shared by log-stream polling commands. </summary>
/// <param name="PollInterval"> The validated polling interval. </param>
/// <param name="IdleTimeout"> The validated idle-timeout threshold when stream mode is enabled. </param>
/// <param name="UntilTimestamp"> The parsed inclusive upper timestamp bound. </param>
internal sealed record LogsStreamRuntimeOptions (
    TimeSpan PollInterval,
    TimeSpan? IdleTimeout,
    DateTimeOffset? UntilTimestamp);