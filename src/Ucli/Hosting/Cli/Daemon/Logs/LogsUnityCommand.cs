using System.Text.Json;
using ConsoleAppFramework;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Provides the logs unity CLI command entry point. </summary>
internal sealed class LogsUnityCommand
{
    private static readonly JsonSerializerOptions JsonLineSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ILogsUnityService logsUnityService;

    /// <summary> Initializes a new instance of the LogsUnityCommand class. </summary>
    /// <param name="logsUnityService"> The Unity-log orchestration service dependency. </param>
    public LogsUnityCommand (ILogsUnityService logsUnityService)
    {
        this.logsUnityService = logsUnityService ?? throw new ArgumentNullException(nameof(logsUnityService));
    }

    /// <summary> Executes the logs unity command. </summary>
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
    [Command(UcliCommandNames.UnitySubcommand)]
    public async Task<int> Unity (
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
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommandExecutionState.MarkStarted();

            if (!LogsOutputFormatCodec.TryParse(format, out var outputFormat, out var formatErrorMessage))
            {
                var invalidFormatResult = CommandResult.InvalidArgument(UcliCommandNames.LogsUnity, formatErrorMessage!);
                CommandResultWriter.WriteToStandardOutput(invalidFormatResult);
                return invalidFormatResult.ExitCode;
            }

            var serviceResult = await logsUnityService.Execute(
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
                    (unityLogEvent, nextCursor, callbackCancellationToken) =>
                    {
                        callbackCancellationToken.ThrowIfCancellationRequested();
                        WriteLogEvent(outputFormat!, unityLogEvent, nextCursor);
                        return ValueTask.CompletedTask;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (!serviceResult.IsSuccess)
            {
                var commandResult = CommandResultFactory.FromExecutionError(UcliCommandNames.LogsUnity, serviceResult.Error!);
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

    private static void WriteLogEvent (
        string format,
        IpcUnityLogEvent unityLogEvent,
        string nextCursor)
    {
        if (string.Equals(format, LogsOutputFormatCodec.Json, StringComparison.Ordinal))
        {
            var jsonLine = JsonSerializer.Serialize(
                new JsonLinePayload(
                    Timestamp: unityLogEvent.Timestamp,
                    Level: unityLogEvent.Level,
                    Source: unityLogEvent.Source,
                    Message: unityLogEvent.Message,
                    StackTrace: unityLogEvent.StackTrace,
                    Cursor: unityLogEvent.Cursor,
                    NextCursor: nextCursor),
                JsonLineSerializerOptions);
            Console.Out.WriteLine(jsonLine);
            return;
        }

        var line = string.Concat(
            unityLogEvent.Timestamp,
            " ",
            unityLogEvent.Level,
            " ",
            unityLogEvent.Source,
            " ",
            LogsTextUtilities.NormalizeSingleLine(unityLogEvent.Message));
        if (string.IsNullOrWhiteSpace(unityLogEvent.StackTrace))
        {
            Console.Out.WriteLine(line);
            return;
        }

        Console.Out.WriteLine(string.Concat(
            line,
            " | ",
            LogsTextUtilities.NormalizeSingleLine(unityLogEvent.StackTrace)));
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
