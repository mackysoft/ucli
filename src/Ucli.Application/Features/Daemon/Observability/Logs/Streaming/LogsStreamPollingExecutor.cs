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
                return LogsReadServiceResult.Failure(error);
            }

            var response = getResponse(readResult);
            if (response is null)
            {
                return LogsReadServiceResult.Failure(ExecutionError.InternalError(
                    "Log read client returned neither a response payload nor an error."));
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
            }

            if (!stream)
            {
                return LogsReadServiceResult.Success();
            }

            var now = DateTimeOffset.UtcNow;
            if (streamTerminationPolicy.ShouldStop(
                    events,
                    now,
                    streamOptions.UntilTimestamp,
                    lastEventTimestamp,
                    streamOptions.IdleTimeout,
                    getTimestamp))
            {
                return LogsReadServiceResult.Success();
            }

            query = withAfter(query, nextCursor);
            await Task.Delay(streamOptions.PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }
}
