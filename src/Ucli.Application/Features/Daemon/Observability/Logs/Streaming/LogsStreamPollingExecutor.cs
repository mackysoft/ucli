using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;

/// <summary> Executes shared polling orchestration for log-read commands. </summary>
internal sealed class LogsStreamPollingExecutor
{
    private readonly IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes one log-stream polling executor. </summary>
    public LogsStreamPollingExecutor (
        IDaemonCommandExecutionContextResolver daemonCommandExecutionContextResolver,
        TimeProvider timeProvider)
    {
        this.daemonCommandExecutionContextResolver = daemonCommandExecutionContextResolver ?? throw new ArgumentNullException(nameof(daemonCommandExecutionContextResolver));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Executes one polling workflow. </summary>
    public async ValueTask<LogsReadServiceResult> ExecuteAsync<TQuery, TReadResult, TResponse, TEvent> (
        UcliCommand commandId,
        string? projectPath,
        int? timeoutMilliseconds,
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
        Func<TEvent, DateTimeOffset> getTimestamp,
        CancellationToken cancellationToken)
        where TQuery : class
        where TResponse : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(commandId);
        ArgumentNullException.ThrowIfNull(initialQuery);
        ArgumentNullException.ThrowIfNull(streamOptions);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(getResponse);
        ArgumentNullException.ThrowIfNull(getError);
        ArgumentNullException.ThrowIfNull(withAfter);
        ArgumentNullException.ThrowIfNull(getEvents);
        ArgumentNullException.ThrowIfNull(getNextCursor);
        ArgumentNullException.ThrowIfNull(getEventCursor);
        ArgumentNullException.ThrowIfNull(onEvent);
        ArgumentNullException.ThrowIfNull(getTimestamp);

        var contextResolutionResult = await daemonCommandExecutionContextResolver.ResolveAsync(
                commandId,
                projectPath,
                timeoutMilliseconds,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResolutionResult.IsSuccess)
        {
            return LogsReadServiceResult.Failure(contextResolutionResult.Error!, 0, null);
        }

        var executionContext = contextResolutionResult.Context!;
        var query = initialQuery;
        var lastEventObservedTimestamp = timeProvider.GetTimestamp();
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
                        // Treat it as an empty poll and decide whether idleTimeout
                        // or untilReached has been reached; bounded non-stream reads still surface the timeout.
                        var timeoutStopResult = GetStreamStopResult(
                            Array.Empty<TEvent>(),
                            lastEventObservedTimestamp,
                            streamOptions,
                            emittedCount,
                            latestNextCursor,
                            getTimestamp);
                        if (timeoutStopResult is not null)
                        {
                            return timeoutStopResult;
                        }

                        await TimeProviderDelay.DelayAsync(streamOptions.PollInterval, timeProvider, cancellationToken).ConfigureAwait(false);
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
                    lastEventObservedTimestamp = timeProvider.GetTimestamp();
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
                    return LogsReadServiceResult.Completed(emittedCount, latestNextCursor);
                }

                var stopResult = GetStreamStopResult(
                    events,
                    lastEventObservedTimestamp,
                    streamOptions,
                    emittedCount,
                    latestNextCursor,
                    getTimestamp);
                if (stopResult is not null)
                {
                    return stopResult;
                }

                query = withAfter(query, nextCursor);
                await TimeProviderDelay.DelayAsync(streamOptions.PollInterval, timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return LogsReadServiceResult.Canceled(emittedCount, latestNextCursor);
        }
    }

    private LogsReadServiceResult? GetStreamStopResult<TEvent> (
        IReadOnlyList<TEvent> events,
        long lastEventObservedTimestamp,
        LogsStreamRuntimeOptions streamOptions,
        int emittedCount,
        string? nextCursor,
        Func<TEvent, DateTimeOffset> getTimestamp)
    {
        if (streamOptions.UntilTimestamp is DateTimeOffset untilTimestamp
            && ShouldStopByUntil(events, untilTimestamp, timeProvider.GetUtcNow(), getTimestamp))
        {
            return LogsReadServiceResult.UntilReached(emittedCount, nextCursor);
        }

        if (streamOptions.IdleTimeout is TimeSpan idleTimeout
            && events.Count == 0
            && timeProvider.GetElapsedTime(lastEventObservedTimestamp) >= idleTimeout)
        {
            return LogsReadServiceResult.IdleTimeout(emittedCount, nextCursor);
        }

        return null;
    }

    private static bool ShouldStopByUntil<TEvent> (
        IReadOnlyList<TEvent> events,
        DateTimeOffset untilTimestamp,
        DateTimeOffset now,
        Func<TEvent, DateTimeOffset> getTimestamp)
    {
        if (events.Count == 0)
        {
            return now >= untilTimestamp;
        }

        foreach (var logEvent in events)
        {
            if (getTimestamp(logEvent) >= untilTimestamp)
            {
                return true;
            }
        }

        return false;
    }
}
