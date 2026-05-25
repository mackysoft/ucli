using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;

/// <summary> Executes shared polling orchestration for log-read commands. </summary>
internal static class LogsStreamPollingExecutor
{
    /// <summary> Executes one polling workflow. </summary>
    public static async ValueTask<LogsReadServiceResult> ExecuteAsync<TQuery, TReadResult, TResponse, TEvent> (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        UcliCommand commandId,
        string? projectPath,
        TQuery initialQuery,
        bool stream,
        LogsStreamRuntimeOptions streamOptions,
        Func<ResolvedUnityProjectContext, TQuery, TimeSpan, CancellationToken, ValueTask<TReadResult>> read,
        Func<TReadResult, TResponse?> getResponse,
        Func<TReadResult, ExecutionError?> getError,
        Func<TQuery, string, TQuery> withAfter,
        Func<TResponse, IReadOnlyList<TEvent>> getEvents,
        Func<TResponse, string> getNextCursor,
        Func<TEvent, string> getEventCursor,
        Func<TEvent, string, CancellationToken, ValueTask> onEvent,
        IDaemonLogsStreamTerminationPolicy streamTerminationPolicy,
        Func<TEvent, string> getTimestamp,
        CancellationToken cancellationToken = default)
        where TQuery : class
        where TResponse : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(daemonCommandExecutionContextResolver);
        ArgumentNullException.ThrowIfNull(initialQuery);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(getResponse);
        ArgumentNullException.ThrowIfNull(getError);
        ArgumentNullException.ThrowIfNull(withAfter);
        ArgumentNullException.ThrowIfNull(getEvents);
        ArgumentNullException.ThrowIfNull(getNextCursor);
        ArgumentNullException.ThrowIfNull(getEventCursor);
        ArgumentNullException.ThrowIfNull(onEvent);
        ArgumentNullException.ThrowIfNull(streamTerminationPolicy);
        ArgumentNullException.ThrowIfNull(getTimestamp);

        var contextResolutionResult = await daemonCommandExecutionContextResolver.ResolveAsync(
                commandId,
                projectPath,
                timeoutMilliseconds: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResolutionResult.IsSuccess)
        {
            return LogsReadServiceResult.Failure(contextResolutionResult.Error!);
        }

        var executionContext = contextResolutionResult.Context!;
        var query = initialQuery;
        var lastEventTimestamp = DateTimeOffset.UtcNow;
        var emittedCount = 0;
        string? latestNextCursor = null;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var readResult = await read(
                        executionContext.Context.UnityProject,
                        query,
                        executionContext.Timeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                var error = getError(readResult);
                if (error is not null)
                {
                    if (stream && error.Kind == ExecutionErrorKind.Timeout)
                    {
                        // NOTE: In stream mode, one bounded poll can time out while no matching log entries exist.
                        // Treat it as an empty poll and let the stream termination policy decide whether idleTimeout
                        // or untilReached has been reached; bounded non-stream reads still surface the timeout.
                        var timeoutStopReason = streamTerminationPolicy.GetStopReason(
                            Array.Empty<TEvent>(),
                            DateTimeOffset.UtcNow,
                            streamOptions.UntilTimestamp,
                            lastEventTimestamp,
                            streamOptions.IdleTimeout,
                            getTimestamp);
                        if (timeoutStopReason is not null)
                        {
                            return LogsReadServiceResult.Success(emittedCount, latestNextCursor, timeoutStopReason);
                        }

                        await Task.Delay(streamOptions.PollInterval, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return LogsReadServiceResult.Failure(error, emittedCount, latestNextCursor);
                }

                var response = getResponse(readResult);
                if (response is null)
                {
                    return LogsReadServiceResult.Failure(
                        ExecutionError.InternalError("Log read client returned neither a response payload nor an error."),
                        emittedCount,
                        latestNextCursor);
                }
                var events = getEvents(response);
                if (events.Count > 0)
                {
                    lastEventTimestamp = DateTimeOffset.UtcNow;
                }

                var nextCursor = getNextCursor(response);
                foreach (var logEvent in events)
                {
                    await onEvent(logEvent, nextCursor, cancellationToken).ConfigureAwait(false);
                    emittedCount++;
                    latestNextCursor = getEventCursor(logEvent);
                }
                latestNextCursor = nextCursor;

                if (!stream)
                {
                    return LogsReadServiceResult.Success(emittedCount, latestNextCursor);
                }

                var now = DateTimeOffset.UtcNow;
                var stopReason = streamTerminationPolicy.GetStopReason(
                    events,
                    now,
                    streamOptions.UntilTimestamp,
                    lastEventTimestamp,
                    streamOptions.IdleTimeout,
                    getTimestamp);
                if (stopReason is not null)
                {
                    return LogsReadServiceResult.Success(emittedCount, latestNextCursor, stopReason);
                }

                query = withAfter(query, nextCursor);
                await Task.Delay(streamOptions.PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return LogsReadServiceResult.Canceled(emittedCount, latestNextCursor);
        }
    }

}
