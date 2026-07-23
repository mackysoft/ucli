using System.Globalization;
using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Infrastructure.Text;

namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Provides the logs daemon read CLI command entry point. </summary>
internal sealed class LogsDaemonReadCommand
{
    private readonly ILogsDaemonService logsDaemonService;

    private readonly ICommandResultWriter commandResultWriter;

    private readonly CliStreamEntryWriterFactory streamEntryWriterFactory;

    /// <summary> Initializes a new instance of the LogsDaemonReadCommand class. </summary>
    /// <param name="logsDaemonService"> The daemon-log orchestration service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    /// <param name="streamEntryWriterFactory"> The per-invocation standard-error stream writer factory. </param>
    public LogsDaemonReadCommand (
        ILogsDaemonService logsDaemonService,
        ICommandResultWriter commandResultWriter,
        CliStreamEntryWriterFactory streamEntryWriterFactory)
    {
        this.logsDaemonService = logsDaemonService ?? throw new ArgumentNullException(nameof(logsDaemonService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
        this.streamEntryWriterFactory = streamEntryWriterFactory ?? throw new ArgumentNullException(nameof(streamEntryWriterFactory));
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
    /// <param name="timeout"> Timeout in milliseconds. </param>
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
        string? timeout = null,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        return await LogsReadCommandExecutor.ExecuteAsync<IpcDaemonLogEvent, JsonLinePayload>(
                UcliCommandNames.LogsDaemonRead,
                format,
                timeout,
                commandResultWriter,
                streamEntryWriterFactory,
                (timeoutMilliseconds, onLogEvent, executeCancellationToken) => logsDaemonService.ExecuteAsync(
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
                        IdleTimeoutMilliseconds: idleTimeoutMilliseconds)
                    {
                        TimeoutMilliseconds = timeoutMilliseconds,
                    },
                    onLogEvent,
                    executeCancellationToken),
                CreateProgressEntry,
                new TextProjector(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Creates one daemon-log progress entry. </summary>
    /// <param name="daemonLogEvent"> The daemon log event payload. </param>
    /// <param name="nextCursor"> The next cursor value returned by daemon. </param>
    private static CliCommandProgressEntry<JsonLinePayload> CreateProgressEntry (
        IpcDaemonLogEvent daemonLogEvent,
        string nextCursor)
    {
        return new CliCommandProgressEntry<JsonLinePayload>(
            "logs.daemon.entry",
            new JsonLinePayload(
                Timestamp: daemonLogEvent.Timestamp.ToString("O", CultureInfo.InvariantCulture),
                Level: TextVocabulary.GetText(daemonLogEvent.Level),
                Category: daemonLogEvent.Category,
                Message: daemonLogEvent.Message,
                Raw: daemonLogEvent.Raw,
                Cursor: daemonLogEvent.Cursor.Value,
                NextCursor: nextCursor));
    }

    private static string CreateTextLine (JsonLinePayload payload)
    {
        var length = checked(
            payload.Timestamp.Length
            + 1
            + payload.Level.Length
            + 1
            + payload.Category.Length
            + 1
            + payload.Message.Length);

        return string.Create(
            length,
            payload,
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(state.Timestamp);
                writer.Append(' ');
                writer.Append(state.Level);
                writer.Append(' ');
                writer.Append(state.Category);
                writer.Append(' ');
                writer.Append(state.Message);
            });
    }

    private sealed class TextProjector : ICliCommandProgressTextProjector
    {
        public bool TryCreateTextEntry<TPayload> (
            string eventName,
            TPayload payload,
            out string text)
            where TPayload : notnull
        {
            if (payload is JsonLinePayload daemonLogEvent)
            {
                text = CreateTextLine(daemonLogEvent);
                return true;
            }

            text = CliProgressTextFormatter.CreateDelimitedEntry(eventName, " ", payload);
            return true;
        }
    }

    /// <summary> Represents one NDJSON output line for daemon log events. </summary>
    /// <param name="Timestamp"> The event timestamp in ISO 8601 format. </param>
    /// <param name="Level"> The event level. </param>
    /// <param name="Category"> The event category. </param>
    /// <param name="Message"> The normalized event message. </param>
    /// <param name="Raw"> The optional raw event payload. </param>
    /// <param name="Cursor"> The event cursor. </param>
    /// <param name="NextCursor"> The next incremental cursor. </param>
    private readonly record struct JsonLinePayload (
        string Timestamp,
        string Level,
        string Category,
        string Message,
        string? Raw,
        string Cursor,
        string NextCursor);
}
