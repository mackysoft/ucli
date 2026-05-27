using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Infrastructure.Text;

namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Provides the logs unity read CLI command entry point. </summary>
internal sealed class LogsUnityReadCommand
{
    private readonly ILogsUnityService logsUnityService;

    private readonly ICommandResultWriter commandResultWriter;

    /// <summary> Initializes a new instance of the LogsUnityReadCommand class. </summary>
    /// <param name="logsUnityService"> The Unity-log orchestration service dependency. </param>
    /// <param name="commandResultWriter"> The command-result writer dependency. </param>
    public LogsUnityReadCommand (
        ILogsUnityService logsUnityService,
        ICommandResultWriter commandResultWriter)
    {
        this.logsUnityService = logsUnityService ?? throw new ArgumentNullException(nameof(logsUnityService));
        this.commandResultWriter = commandResultWriter ?? throw new ArgumentNullException(nameof(commandResultWriter));
    }

    /// <summary> Executes the logs unity read command. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path. When omitted, the current working directory is used.</param>
    /// <param name="tail"> The optional tail count. </param>
    /// <param name="after"> The optional opaque cursor used for incremental reads. </param>
    /// <param name="since"> The optional lower time bound in ISO 8601 format. </param>
    /// <param name="until"> The optional upper time bound in ISO 8601 format. </param>
    /// <param name="level"> The optional level filter (error|warning|info|all). </param>
    /// <param name="query"> The optional free-text query value. </param>
    /// <param name="queryTarget"> --queryTarget, The optional query target (message|stack|both). </param>
    /// <param name="source"> The optional source filter (compile|runtime|all). </param>
    /// <param name="stackTrace"> --stackTrace, The optional stack-trace mode (none|error|all). </param>
    /// <param name="stackTraceMaxFrames"> --stackTraceMaxFrames, The optional stack-trace frame cap applied after stack-trace filtering. </param>
    /// <param name="stackTraceMaxChars"> --stackTraceMaxChars, The optional stack-trace character cap applied after stack-trace filtering. </param>
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
        string? source = null,
        string? stackTrace = null,
        int? stackTraceMaxFrames = null,
        int? stackTraceMaxChars = null,
        bool stream = false,
        int? pollIntervalMilliseconds = null,
        int? idleTimeoutMilliseconds = null,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        return await LogsReadCommandExecutor.ExecuteAsync<IpcUnityLogEvent>(
                UcliCommandNames.LogsUnityRead,
                format,
                commandResultWriter,
                (onLogEvent, executeCancellationToken) => logsUnityService.ExecuteAsync(
                    new LogsUnityServiceRequest(
                        ProjectPath: projectPath,
                        Tail: tail,
                        After: after,
                        Since: since,
                        Until: until,
                        Level: level,
                        Query: query,
                        QueryTarget: queryTarget,
                        Source: source,
                        StackTrace: stackTrace,
                        StackTraceMaxFrames: stackTraceMaxFrames,
                        StackTraceMaxChars: stackTraceMaxChars,
                        Stream: stream,
                        PollIntervalMilliseconds: pollIntervalMilliseconds,
                        IdleTimeoutMilliseconds: idleTimeoutMilliseconds),
                    onLogEvent,
                    executeCancellationToken),
                CreateProgressEntry,
                new TextProjector(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static CliCommandProgressEntry CreateProgressEntry (
        IpcUnityLogEvent unityLogEvent,
        string nextCursor)
    {
        return new CliCommandProgressEntry(
            "logs.unity.entry",
            new JsonLinePayload(
                Timestamp: unityLogEvent.Timestamp,
                Level: unityLogEvent.Level,
                Source: unityLogEvent.Source,
                Message: unityLogEvent.Message,
                StackTrace: unityLogEvent.StackTrace,
                Cursor: unityLogEvent.Cursor,
                NextCursor: nextCursor));
    }

    private static string CreateTextLine (JsonLinePayload payload)
    {
        var hasStackTrace = !string.IsNullOrWhiteSpace(payload.StackTrace);
        var length = checked(
            payload.Timestamp.Length
            + 1
            + payload.Level.Length
            + 1
            + payload.Source.Length
            + 1
            + payload.Message.Length
            + (hasStackTrace ? 3 + payload.StackTrace!.Length : 0));

        return string.Create(
            length,
            (payload.Timestamp, payload.Level, payload.Source, payload.Message, HasStackTrace: hasStackTrace, StackTrace: payload.StackTrace ?? string.Empty),
            static (destination, state) =>
            {
                var writer = new SpanTextWriter(destination);
                writer.Append(state.Timestamp);
                writer.Append(' ');
                writer.Append(state.Level);
                writer.Append(' ');
                writer.Append(state.Source);
                writer.Append(' ');
                writer.Append(state.Message);
                if (state.HasStackTrace)
                {
                    writer.Append(" | ");
                    writer.Append(state.StackTrace);
                }
            });
    }

    private sealed class TextProjector : ICliCommandProgressTextProjector
    {
        public bool TryCreateTextEntry (
            string eventName,
            object payload,
            out string text)
        {
            if (payload is JsonLinePayload unityLogEvent)
            {
                text = CreateTextLine(unityLogEvent);
                return true;
            }

            text = string.Concat(eventName, " ", payload);
            return true;
        }
    }

    /// <summary> Represents one NDJSON output line for Unity log events. </summary>
    private sealed record JsonLinePayload (
        string Timestamp,
        string Level,
        string Source,
        string Message,
        string? StackTrace,
        string Cursor,
        string NextCursor);
}
