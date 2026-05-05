using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;

/// <summary> Validates polling options shared by log-stream CLI commands. </summary>
internal static class LogsStreamRuntimeOptionsValidator
{
    private const int DefaultPollIntervalMilliseconds = 300;

    private const int MinimumPollIntervalMilliseconds = 50;

    private const int MaximumPollIntervalMilliseconds = 60000;

    /// <summary> Tries to validate common stream runtime options. </summary>
    /// <param name="stream"> Indicates whether stream polling is enabled. </param>
    /// <param name="pollIntervalMilliseconds"> The optional polling interval in milliseconds. </param>
    /// <param name="idleTimeoutMilliseconds"> The optional idle-timeout threshold in milliseconds. </param>
    /// <param name="untilTimestamp"> The parsed inclusive upper timestamp bound. </param>
    /// <param name="streamOptions"> The validated runtime options when validation succeeds. </param>
    /// <param name="error"> The invalid-argument error when validation fails. </param>
    /// <returns> <see langword="true" /> when validation succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryValidate (
        bool stream,
        int? pollIntervalMilliseconds,
        int? idleTimeoutMilliseconds,
        DateTimeOffset? untilTimestamp,
        out LogsStreamRuntimeOptions? streamOptions,
        out ExecutionError? error)
    {
        streamOptions = null;

        if (!stream)
        {
            if (pollIntervalMilliseconds.HasValue)
            {
                error = ExecutionError.InvalidArgument("pollIntervalMilliseconds can be used only when stream is enabled.");
                return false;
            }

            if (idleTimeoutMilliseconds.HasValue)
            {
                error = ExecutionError.InvalidArgument("idleTimeoutMilliseconds can be used only when stream is enabled.");
                return false;
            }
        }

        if (pollIntervalMilliseconds.HasValue
            && (pollIntervalMilliseconds.Value < MinimumPollIntervalMilliseconds
                || pollIntervalMilliseconds.Value > MaximumPollIntervalMilliseconds))
        {
            error = ExecutionError.InvalidArgument(
                $"pollIntervalMilliseconds must be between {MinimumPollIntervalMilliseconds} and {MaximumPollIntervalMilliseconds}. Actual: {pollIntervalMilliseconds.Value}.");
            return false;
        }

        if (idleTimeoutMilliseconds.HasValue && idleTimeoutMilliseconds.Value <= 0)
        {
            error = ExecutionError.InvalidArgument(
                $"idleTimeoutMilliseconds must be greater than zero. Actual: {idleTimeoutMilliseconds.Value}.");
            return false;
        }

        streamOptions = new LogsStreamRuntimeOptions(
            PollInterval: TimeSpan.FromMilliseconds(pollIntervalMilliseconds ?? DefaultPollIntervalMilliseconds),
            IdleTimeout: idleTimeoutMilliseconds.HasValue
                ? TimeSpan.FromMilliseconds(idleTimeoutMilliseconds.Value)
                : (TimeSpan?)null,
            UntilTimestamp: untilTimestamp);
        error = null;
        return true;
    }
}
