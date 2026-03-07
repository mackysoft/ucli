using System.Text.Json;
using ConsoleAppFramework;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Logs;

namespace MackySoft.Ucli.Cli;

/// <summary> Provides the <c>logs daemon</c> CLI command entry point. </summary>
internal sealed class LogsDaemonCommand
{
    private static readonly JsonSerializerOptions JsonLineSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ILogsDaemonService logsDaemonService;

    /// <summary> Initializes a new instance of the <see cref="LogsDaemonCommand" /> class. </summary>
    /// <param name="logsDaemonService"> The daemon-log orchestration service dependency. </param>
    public LogsDaemonCommand (ILogsDaemonService logsDaemonService)
    {
        this.logsDaemonService = logsDaemonService ?? throw new ArgumentNullException(nameof(logsDaemonService));
    }

    /// <summary> Executes the <c>logs daemon</c> command. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="tail"> The optional tail count. </param>
    /// <param name="after"> The optional opaque cursor used for incremental reads. </param>
    /// <param name="since"> The optional lower time bound in ISO 8601 format. </param>
    /// <param name="until"> The optional upper time bound in ISO 8601 format. </param>
    /// <param name="level"> The optional level filter (<c>error|warning|info|all</c>). </param>
    /// <param name="query"> The optional free-text query value. </param>
    /// <param name="queryTarget"> --queryTarget, The optional query target (<c>message|stack|both</c>). </param>
    /// <param name="category"> The optional category filter. </param>
    /// <param name="stream"> Enables stream polling mode until canceled or timeout conditions are met. </param>
    /// <param name="pollIntervalMilliseconds"> --pollIntervalMilliseconds, The optional polling interval in milliseconds used when <paramref name="stream" /> is enabled. </param>
    /// <param name="idleTimeoutMilliseconds"> --idleTimeoutMilliseconds, The optional idle timeout in milliseconds used when <paramref name="stream" /> is enabled. </param>
    /// <param name="format"> The output format (<c>text|json</c>). </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The command exit code. </returns>
    [Command(UcliCommandNames.Daemon)]
    public async Task<int> Daemon (
        string? projectPath = null,
        int? tail = null,
        string? after = null,
        string? since = null,
        string? until = null,
        string? level = null,
        string? query = null,
        string? queryTarget = null,
        string? category = null,
        bool stream = false,
        int? pollIntervalMilliseconds = null,
        int? idleTimeoutMilliseconds = null,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommandExecutionState.MarkStarted();

            if (!LogsOutputFormatCodec.TryParse(format, out var outputFormat, out var formatErrorMessage))
            {
                var invalidFormatResult = CommandResult.InvalidArgument(UcliCommandNames.LogsDaemon, formatErrorMessage!);
                CommandResultWriter.WriteToStandardOutput(invalidFormatResult);
                return invalidFormatResult.ExitCode;
            }

            var serviceResult = await logsDaemonService.Execute(
                    new LogsDaemonServiceRequest(
                        ProjectPath: projectPath,
                        Tail: tail,
                        After: after,
                        Since: since,
                        Until: until,
                        Level: level,
                        Query: query,
                        QueryTarget: queryTarget,
                        Category: category,
                        Stream: stream,
                        PollIntervalMilliseconds: pollIntervalMilliseconds,
                        IdleTimeoutMilliseconds: idleTimeoutMilliseconds),
                    (daemonLogEvent, nextCursor, callbackCancellationToken) =>
                    {
                        callbackCancellationToken.ThrowIfCancellationRequested();
                        WriteLogEvent(outputFormat!, daemonLogEvent, nextCursor);
                        return ValueTask.CompletedTask;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (!serviceResult.IsSuccess)
            {
                var commandResult = CommandResultFactory.FromExecutionError(UcliCommandNames.LogsDaemon, serviceResult.Error!);
                CommandResultWriter.WriteToStandardOutput(commandResult);
                return commandResult.ExitCode;
            }

            return (int)CliExitCode.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (int)CliExitCode.Success;
        }
    }

    /// <summary> Writes one daemon log event to standard output by selected format. </summary>
    /// <param name="format"> The normalized output format. </param>
    /// <param name="daemonLogEvent"> The daemon log event payload. </param>
    /// <param name="nextCursor"> The next cursor value returned by daemon. </param>
    private static void WriteLogEvent (
        string format,
        IpcDaemonLogEvent daemonLogEvent,
        string nextCursor)
    {
        if (string.Equals(format, LogsOutputFormatCodec.Json, StringComparison.Ordinal))
        {
            var jsonLine = JsonSerializer.Serialize(
                new JsonLinePayload(
                    Timestamp: daemonLogEvent.Timestamp,
                    Level: daemonLogEvent.Level,
                    Category: daemonLogEvent.Category,
                    Message: daemonLogEvent.Message,
                    Raw: daemonLogEvent.Raw,
                    Cursor: daemonLogEvent.Cursor,
                    NextCursor: nextCursor),
                JsonLineSerializerOptions);
            Console.Out.WriteLine(jsonLine);
            return;
        }

        var textLine = string.Concat(
            daemonLogEvent.Timestamp,
            " ",
            daemonLogEvent.Level,
            " ",
            daemonLogEvent.Category,
            " ",
            LogsTextUtilities.NormalizeSingleLine(daemonLogEvent.Message));
        Console.Out.WriteLine(textLine);
    }

    /// <summary> Represents one NDJSON output line for daemon log events. </summary>
    /// <param name="Timestamp"> The event timestamp in ISO 8601 format. </param>
    /// <param name="Level"> The event level. </param>
    /// <param name="Category"> The event category. </param>
    /// <param name="Message"> The normalized event message. </param>
    /// <param name="Raw"> The optional raw event payload. </param>
    /// <param name="Cursor"> The event cursor. </param>
    /// <param name="NextCursor"> The next incremental cursor. </param>
    private sealed record JsonLinePayload (
        string Timestamp,
        string Level,
        string Category,
        string Message,
        string? Raw,
        string Cursor,
        string NextCursor);
}