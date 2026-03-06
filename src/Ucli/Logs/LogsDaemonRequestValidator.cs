using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Logs;

/// <summary> Validates raw <c>logs daemon</c> request values. </summary>
internal sealed class LogsDaemonRequestValidator : ILogsDaemonRequestValidator
{
    private const int DefaultPollIntervalMilliseconds = 300;

    private const int MinimumPollIntervalMilliseconds = 50;

    private const int MaximumPollIntervalMilliseconds = 60000;

    /// <inheritdoc />
    public bool TryValidate (
        LogsDaemonServiceRequest request,
        [NotNullWhen(true)]
        out LogsDaemonValidatedRequest? validatedRequest,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(request);

        validatedRequest = null;

        if (request.Tail.HasValue && request.Tail.Value <= 0)
        {
            error = ExecutionError.InvalidArgument($"tail must be greater than zero. Actual: {request.Tail.Value}.");
            return false;
        }

        if (!request.Stream)
        {
            if (request.PollIntervalMilliseconds.HasValue)
            {
                error = ExecutionError.InvalidArgument("pollIntervalMilliseconds can be used only when stream is enabled.");
                return false;
            }

            if (request.IdleTimeoutMilliseconds.HasValue)
            {
                error = ExecutionError.InvalidArgument("idleTimeoutMilliseconds can be used only when stream is enabled.");
                return false;
            }
        }

        if (request.PollIntervalMilliseconds.HasValue
            && (request.PollIntervalMilliseconds.Value < MinimumPollIntervalMilliseconds
                || request.PollIntervalMilliseconds.Value > MaximumPollIntervalMilliseconds))
        {
            error = ExecutionError.InvalidArgument(
                $"pollIntervalMilliseconds must be between {MinimumPollIntervalMilliseconds} and {MaximumPollIntervalMilliseconds}. Actual: {request.PollIntervalMilliseconds.Value}.");
            return false;
        }

        if (request.IdleTimeoutMilliseconds.HasValue && request.IdleTimeoutMilliseconds.Value <= 0)
        {
            error = ExecutionError.InvalidArgument(
                $"idleTimeoutMilliseconds must be greater than zero. Actual: {request.IdleTimeoutMilliseconds.Value}.");
            return false;
        }

        if (!IpcDaemonLogsQueryTargetCodec.TryParseForDaemonLogs(
                request.QueryTarget,
                out _,
                out var queryTargetValidationError))
        {
            error = ExecutionError.InvalidArgument(
                queryTargetValidationError!);
            return false;
        }

        if (!TryValidateTimeRange(request.Since, request.Until, out _, out var untilTimestamp, out error))
        {
            return false;
        }

        var pollIntervalMilliseconds = request.PollIntervalMilliseconds ?? DefaultPollIntervalMilliseconds;
        var pollInterval = TimeSpan.FromMilliseconds(pollIntervalMilliseconds);
        var idleTimeout = request.IdleTimeoutMilliseconds.HasValue
            ? TimeSpan.FromMilliseconds(request.IdleTimeoutMilliseconds.Value)
            : (TimeSpan?)null;

        validatedRequest = new LogsDaemonValidatedRequest(
            PollInterval: pollInterval,
            IdleTimeout: idleTimeout,
            UntilTimestamp: untilTimestamp);
        error = null;
        return true;
    }

    /// <summary> Validates time-range input values for <c>since</c> and <c>until</c> options. </summary>
    /// <param name="since"> The optional lower time bound string. </param>
    /// <param name="until"> The optional upper time bound string. </param>
    /// <param name="sinceTimestamp"> The parsed lower timestamp when parse succeeds. </param>
    /// <param name="untilTimestamp"> The parsed upper timestamp when parse succeeds. </param>
    /// <param name="error"> The invalid-argument error when validation fails. </param>
    /// <returns> <see langword="true" /> when values are valid; otherwise <see langword="false" />. </returns>
    private static bool TryValidateTimeRange (
        string? since,
        string? until,
        out DateTimeOffset? sinceTimestamp,
        out DateTimeOffset? untilTimestamp,
        out ExecutionError? error)
    {
        if (!IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(since, out sinceTimestamp))
        {
            untilTimestamp = null;
            error = ExecutionError.InvalidArgument($"since must be an ISO 8601 timestamp with timezone offset. Actual: {since}.");
            return false;
        }

        if (!IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(until, out untilTimestamp))
        {
            error = ExecutionError.InvalidArgument($"until must be an ISO 8601 timestamp with timezone offset. Actual: {until}.");
            return false;
        }

        if (sinceTimestamp.HasValue
            && untilTimestamp.HasValue
            && sinceTimestamp.Value > untilTimestamp.Value)
        {
            error = ExecutionError.InvalidArgument($"since must be less than or equal to until. since={since}, until={until}.");
            return false;
        }

        error = null;
        return true;
    }
}