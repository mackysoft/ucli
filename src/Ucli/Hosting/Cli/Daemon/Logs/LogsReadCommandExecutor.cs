using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

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
        Action<string, TLogEvent, string> writeLogEvent,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentNullException.ThrowIfNull(commandResultWriter);
        ArgumentNullException.ThrowIfNull(executeAsync);
        ArgumentNullException.ThrowIfNull(writeLogEvent);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommandExecutionState.MarkStarted();

            if (!LogsOutputFormatCodec.TryParse(format, out var outputFormat, out var formatErrorMessage))
            {
                var invalidFormatResult = CommandResultFactory.FromExecutionError(
                    commandName,
                    ExecutionError.InvalidArgument(formatErrorMessage!));
                commandResultWriter.WriteToStandardOutput(invalidFormatResult);
                return invalidFormatResult.ExitCode;
            }

            var normalizedOutputFormat = outputFormat!;
            var serviceResult = await executeAsync(
                    (logEvent, nextCursor, callbackCancellationToken) =>
                    {
                        callbackCancellationToken.ThrowIfCancellationRequested();
                        writeLogEvent(normalizedOutputFormat, logEvent, nextCursor);
                        return ValueTask.CompletedTask;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (!serviceResult.IsSuccess)
            {
                var commandResult = CommandResultFactory.FromExecutionError(commandName, serviceResult.Error!);
                commandResultWriter.WriteToStandardOutput(commandResult);
                return commandResult.ExitCode;
            }

            return (int)CliExitCode.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (int)CliExitCode.Success;
        }
    }
}
