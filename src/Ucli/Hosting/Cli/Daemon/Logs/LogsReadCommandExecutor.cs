using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Executes shared <c>logs * read</c> CLI control flow. </summary>
internal static class LogsReadCommandExecutor
{
    /// <summary> Executes one logs read command and writes error envelopes when validation or service execution fails. </summary>
    /// <typeparam name="TLogEvent"> The streamed log event type. </typeparam>
    /// <param name="commandName"> The command name used in emitted error envelopes. </param>
    /// <param name="format"> The requested output format. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <param name="executeAsync"> The service execution delegate. </param>
    /// <param name="writeLogEvent"> The output projection for one log event. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The command exit code. </returns>
    public static async Task<int> ExecuteAsync<TLogEvent> (
        string commandName,
        string? format,
        ICommandResultWriter commandResultWriter,
        Func<Func<TLogEvent, string, CancellationToken, ValueTask>, CancellationToken, ValueTask<LogsReadServiceResult>> executeAsync,
        Func<CliStreamEntryWriter, string, TLogEvent, string, string> writeLogEvent,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(commandResultWriter);
        ArgumentNullException.ThrowIfNull(executeAsync);
        ArgumentNullException.ThrowIfNull(writeLogEvent);

        var emittedCount = 0;
        string? latestNextCursor = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommandExecutionState.MarkStarted();

            if (!CliStreamEntryFormatCodec.TryParse(format, out var outputFormat, out var formatErrorMessage))
            {
                var invalidFormatResult = LogsReadCommandResultFactory.Create(
                    commandName,
                    LogsReadServiceResult.Failure(ExecutionError.InvalidArgument(formatErrorMessage!)));
                commandResultWriter.WriteToStandardOutput(invalidFormatResult);
                return invalidFormatResult.ExitCode;
            }

            var normalizedOutputFormat = outputFormat!;
            var entryWriter = new CliStreamEntryWriter(commandName);
            var serviceResult = await executeAsync(
                    (logEvent, nextCursor, callbackCancellationToken) =>
                    {
                        callbackCancellationToken.ThrowIfCancellationRequested();
                        var confirmedCursor = writeLogEvent(entryWriter, normalizedOutputFormat, logEvent, nextCursor);
                        emittedCount++;
                        latestNextCursor = confirmedCursor;
                        return ValueTask.CompletedTask;
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            var commandResult = LogsReadCommandResultFactory.Create(commandName, serviceResult);
            commandResultWriter.WriteToStandardOutput(commandResult);
            return commandResult.ExitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var commandResult = LogsReadCommandResultFactory.Create(commandName, LogsReadServiceResult.Canceled(
                emittedCount,
                latestNextCursor));
            commandResultWriter.WriteToStandardOutput(commandResult);
            return commandResult.ExitCode;
        }
        catch (Exception exception)
        {
            var commandResult = LogsReadCommandResultFactory.Create(
                commandName,
                LogsReadServiceResult.Failure(
                    ExecutionError.InternalError($"Unexpected error during log read execution: {exception.Message}"),
                    emittedCount,
                    latestNextCursor));
            commandResultWriter.WriteToStandardOutput(commandResult);
            return commandResult.ExitCode;
        }
    }
}
