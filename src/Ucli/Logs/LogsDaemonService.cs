using System.Globalization;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Logs;

/// <summary> Implements polling orchestration for <c>logs daemon</c> command execution. </summary>
internal sealed class LogsDaemonService : ILogsDaemonService
{
    private const int DefaultPollIntervalMilliseconds = 300;

    private const int MinimumPollIntervalMilliseconds = 50;

    private const int MaximumPollIntervalMilliseconds = 60000;

    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly IDaemonLogsClient daemonLogsClient;

    /// <summary> Initializes a new instance of the <see cref="LogsDaemonService" /> class. </summary>
    /// <param name="daemonCommandExecutionContextResolver"> The daemon-command context resolver dependency. </param>
    /// <param name="daemonLogsClient"> The daemon-log IPC client dependency. </param>
    public LogsDaemonService (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        IDaemonLogsClient daemonLogsClient)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.daemonLogsClient = daemonLogsClient ?? throw new ArgumentNullException(nameof(daemonLogsClient));
    }

    /// <inheritdoc />
    public async ValueTask<LogsDaemonServiceResult> Execute (
        LogsDaemonServiceRequest request,
        Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask> onEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onEvent);

        var argumentValidationError = ValidateRequestArguments(request);
        if (argumentValidationError is not null)
        {
            return LogsDaemonServiceResult.Failure(argumentValidationError);
        }

        var contextResolutionResult = await daemonCommandExecutionContextResolver.Resolve(
                UcliCommandIds.LogsDaemon,
                request.ProjectPath,
                timeout: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResolutionResult.IsSuccess)
        {
            return LogsDaemonServiceResult.Failure(contextResolutionResult.Error!);
        }

        var executionContext = contextResolutionResult.Context!;
        var pollIntervalMilliseconds = request.PollIntervalMilliseconds ?? DefaultPollIntervalMilliseconds;
        var pollInterval = TimeSpan.FromMilliseconds(pollIntervalMilliseconds);
        var idleTimeout = request.IdleTimeoutMilliseconds.HasValue
            ? TimeSpan.FromMilliseconds(request.IdleTimeoutMilliseconds.Value)
            : (TimeSpan?)null;

        string? nextAfterCursor = request.After;
        var lastEventTimestamp = DateTimeOffset.UtcNow;
        var untilTimestamp = TryParseTimestamp(request.Until);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readQuery = new DaemonLogsReadQuery(
                Tail: request.Tail,
                After: nextAfterCursor,
                Since: request.Since,
                Until: request.Until,
                Level: request.Level,
                Query: request.Query,
                QueryTarget: request.QueryTarget,
                Category: request.Category);
            var readResult = await daemonLogsClient.Read(
                    executionContext.Context.UnityProject,
                    readQuery,
                    executionContext.Timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!readResult.IsSuccess)
            {
                return LogsDaemonServiceResult.Failure(readResult.Error!);
            }

            var payload = readResult.Response!;
            if (payload.Events.Length > 0)
            {
                lastEventTimestamp = DateTimeOffset.UtcNow;
            }

            foreach (var daemonLogEvent in payload.Events)
            {
                await onEvent(daemonLogEvent, payload.NextCursor, cancellationToken).ConfigureAwait(false);
            }

            if (!request.Stream)
            {
                return LogsDaemonServiceResult.Success();
            }

            nextAfterCursor = payload.NextCursor;

            if (untilTimestamp.HasValue && ShouldStopByUntil(untilTimestamp.Value, payload.Events))
            {
                return LogsDaemonServiceResult.Success();
            }

            if (idleTimeout.HasValue
                && payload.Events.Length == 0
                && DateTimeOffset.UtcNow - lastEventTimestamp >= idleTimeout.Value)
            {
                return LogsDaemonServiceResult.Success();
            }

            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary> Validates command request arguments before resolving context. </summary>
    /// <param name="request"> The request values to validate. </param>
    /// <returns> Structured invalid-argument error when validation fails; otherwise <see langword="null" />. </returns>
    private static ExecutionError? ValidateRequestArguments (LogsDaemonServiceRequest request)
    {
        if (request.Tail.HasValue && request.Tail.Value <= 0)
        {
            return ExecutionError.InvalidArgument($"tail must be greater than zero. Actual: {request.Tail.Value}.");
        }

        if (!request.Stream)
        {
            if (request.PollIntervalMilliseconds.HasValue)
            {
                return ExecutionError.InvalidArgument("pollIntervalMilliseconds can be used only when stream is enabled.");
            }

            if (request.IdleTimeoutMilliseconds.HasValue)
            {
                return ExecutionError.InvalidArgument("idleTimeoutMilliseconds can be used only when stream is enabled.");
            }
        }

        if (request.PollIntervalMilliseconds.HasValue
            && (request.PollIntervalMilliseconds.Value < MinimumPollIntervalMilliseconds
                || request.PollIntervalMilliseconds.Value > MaximumPollIntervalMilliseconds))
        {
            return ExecutionError.InvalidArgument(
                $"pollIntervalMilliseconds must be between {MinimumPollIntervalMilliseconds} and {MaximumPollIntervalMilliseconds}. Actual: {request.PollIntervalMilliseconds.Value}.");
        }

        if (request.IdleTimeoutMilliseconds.HasValue && request.IdleTimeoutMilliseconds.Value <= 0)
        {
            return ExecutionError.InvalidArgument(
                $"idleTimeoutMilliseconds must be greater than zero. Actual: {request.IdleTimeoutMilliseconds.Value}.");
        }

        if (!string.IsNullOrWhiteSpace(request.QueryTarget)
            && string.Equals(request.QueryTarget, "stack", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionError.InvalidArgument("queryTarget 'stack' is not supported for logs daemon. Supported: message, both.");
        }

        if (!TryValidateTimeRange(request.Since, request.Until, out var timeRangeError))
        {
            return timeRangeError;
        }

        return null;
    }

    /// <summary> Validates time-range input values for <c>since</c> and <c>until</c> options. </summary>
    /// <param name="since"> The optional lower time bound string. </param>
    /// <param name="until"> The optional upper time bound string. </param>
    /// <param name="error"> The invalid-argument error when validation fails. </param>
    /// <returns> <see langword="true" /> when values are valid; otherwise <see langword="false" />. </returns>
    private static bool TryValidateTimeRange (
        string? since,
        string? until,
        out ExecutionError? error)
    {
        var sinceTimestamp = TryParseTimestamp(since);
        if (!string.IsNullOrWhiteSpace(since) && !sinceTimestamp.HasValue)
        {
            error = ExecutionError.InvalidArgument($"since must be an ISO 8601 timestamp with timezone offset. Actual: {since}.");
            return false;
        }

        var untilTimestamp = TryParseTimestamp(until);
        if (!string.IsNullOrWhiteSpace(until) && !untilTimestamp.HasValue)
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

    /// <summary> Attempts to parse one ISO 8601 timestamp string. </summary>
    /// <param name="value"> The raw timestamp value. </param>
    /// <returns> Parsed timestamp when successful; otherwise <see langword="null" />. </returns>
    private static DateTimeOffset? TryParseTimestamp (string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedTimestamp))
        {
            return null;
        }

        var normalizedValue = value.Trim();
        var hasOffset = normalizedValue.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
            || normalizedValue.Contains('+')
            || normalizedValue.LastIndexOf('-') > normalizedValue.IndexOf('T');
        if (!hasOffset)
        {
            return null;
        }

        return parsedTimestamp;
    }

    /// <summary> Determines whether stream loop should stop based on <c>until</c> constraint. </summary>
    /// <param name="until"> The inclusive upper timestamp bound. </param>
    /// <param name="events"> The current batch events. </param>
    /// <returns> <see langword="true" /> when stream loop should stop; otherwise <see langword="false" />. </returns>
    private static bool ShouldStopByUntil (
        DateTimeOffset until,
        IReadOnlyList<IpcDaemonLogEvent> events)
    {
        if (events.Count == 0)
        {
            return DateTimeOffset.UtcNow >= until;
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