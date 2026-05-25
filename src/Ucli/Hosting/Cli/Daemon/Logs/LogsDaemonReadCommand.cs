using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Provides the logs daemon read CLI command entry point. </summary>
internal sealed class LogsDaemonReadCommand
{
    private readonly ILogsDaemonService logsDaemonService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the LogsDaemonReadCommand class. </summary>
    /// <param name="logsDaemonService"> The daemon-log orchestration service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public LogsDaemonReadCommand (
        ILogsDaemonService logsDaemonService,
        ICommandResultWriter commandResultWriter)
    {
        this.logsDaemonService = logsDaemonService ?? throw new ArgumentNullException(nameof(logsDaemonService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the logs daemon read command. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="tail"> The optional tail count. </param>
    /// <param name="after"> The optional opaque cursor used for incremental reads. </param>
    /// <param name="since"> The optional lower time bound in ISO 8601 format. </param>
    /// <param name="until"> The optional upper time bound in ISO 8601 format. </param>
    /// <param name="level"> The optional level filter (error|warning|info|all). </param>
    /// <param name="query"> The optional free-text query value. </param>
    /// <param name="queryTarget"> --queryTarget, The optional query target (message|stack|both). </param>
    /// <param name="category"> The optional category filter. </param>
    /// <param name="stream"> Enables stream polling mode until canceled or timeout conditions are met. </param>
    /// <param name="pollIntervalMilliseconds"> --pollIntervalMilliseconds, The optional polling interval in milliseconds used when stream is enabled. </param>
    /// <param name="idleTimeoutMilliseconds"> --idleTimeoutMilliseconds, The optional idle timeout in milliseconds used when stream is enabled. </param>
    /// <param name="format"> The output format (text|json). </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The command exit code. </returns>
    [Command(UcliCommandNames.ReadSubcommand)]
    public async Task<int> ReadAsync (
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
        return await LogsReadCommandExecutor.ExecuteAsync<IpcDaemonLogEvent>(
                UcliCommandNames.LogsDaemonRead,
                format,
                commandResultWriter,
                (onLogEvent, executeCancellationToken) => logsDaemonService.ExecuteAsync(
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
                    onLogEvent,
                    executeCancellationToken),
                WriteLogEvent,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Writes one daemon log event to standard error by selected format. </summary>
    /// <param name="entryWriter"> The stream-entry writer. </param>
    /// <param name="format"> The parsed output format. </param>
    /// <param name="daemonLogEvent"> The daemon log event payload. </param>
    /// <param name="nextCursor"> The next cursor value returned by daemon. </param>
    private static string WriteLogEvent (
        CliStreamEntryWriter entryWriter,
        CliStreamEntryFormat format,
        IpcDaemonLogEvent daemonLogEvent,
        string nextCursor)
    {
        if (format == CliStreamEntryFormat.Json)
        {
            entryWriter.WriteJsonEntry(
                "logs.daemon.entry",
                new JsonLinePayload(
                    Timestamp: daemonLogEvent.Timestamp,
                    Level: daemonLogEvent.Level,
                    Category: daemonLogEvent.Category,
                    Message: daemonLogEvent.Message,
                    Raw: daemonLogEvent.Raw,
                    Cursor: daemonLogEvent.Cursor,
                    NextCursor: nextCursor));
            return nextCursor;
        }

        var textLine = string.Concat(
            daemonLogEvent.Timestamp,
            " ",
            daemonLogEvent.Level,
            " ",
            daemonLogEvent.Category,
            " ",
            LogsTextUtilities.NormalizeSingleLine(daemonLogEvent.Message));
        entryWriter.WriteTextEntry(textLine);
        return nextCursor;
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
