using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Daemon.Logs;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsUnityReadCommandTests
{
    private const string DaemonSessionNotAvailableMessage = "No daemon session is available for the requested project. Start the daemon or check --projectPath.";

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenFormatIsJson_WritesNdjsonEvents ()
    {
        var command = new LogsUnityReadCommand(new RecordingLogsUnityService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                CreateEvent(
                    cursor: "stream-1:1",
                    message: "compile warning",
                    source: "compile",
                    stackTrace: null),
                "stream-1:3",
                cancellationToken);
            await onEvent(
                CreateEvent(
                    cursor: "stream-1:2",
                    message: "runtime error",
                    source: "runtime",
                    stackTrace: "at Player.Run()"),
                "stream-1:3",
                cancellationToken);
            return LogsReadServiceResult.Success(count: 2, nextCursor: "stream-1:3");
        }), CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(format: "json"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        AssertSuccessResult(standardOutput, count: 2, nextCursor: "stream-1:3");
        var lines = standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var firstLine = JsonDocument.Parse(lines[0]);
        using var secondLine = JsonDocument.Parse(lines[1]);
        AssertEntryEnvelope(firstLine.RootElement, sequence: 1);
        AssertEntryEnvelope(secondLine.RootElement, sequence: 2);
        var firstPayload = firstLine.RootElement.GetProperty("payload");
        var secondPayload = secondLine.RootElement.GetProperty("payload");
        Assert.Equal("compile", firstPayload.GetProperty("source").GetString());
        Assert.Equal("compile warning", firstPayload.GetProperty("message").GetString());
        Assert.Equal("stream-1:1", firstPayload.GetProperty("cursor").GetString());
        Assert.Equal("stream-1:3", firstPayload.GetProperty("nextCursor").GetString());
        Assert.Equal("runtime", secondPayload.GetProperty("source").GetString());
        Assert.Equal("at Player.Run()", secondPayload.GetProperty("stackTrace").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenFormatIsText_WritesSingleLineEventsWithStackTraceSuffix ()
    {
        var command = new LogsUnityReadCommand(new RecordingLogsUnityService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                CreateEvent(
                    cursor: "stream-1:1",
                    message: "line 1\nline 2",
                    source: "runtime",
                    stackTrace: "frame 1\nframe 2"),
                "stream-1:2",
                cancellationToken);
            return LogsReadServiceResult.Success(count: 1, nextCursor: "stream-1:2");
        }), CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(format: "text"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        AssertSuccessResult(standardOutput, count: 1, nextCursor: "stream-1:2");
        Assert.Equal(
            "2026-03-05T10:30:00+09:00 info runtime line 1\\nline 2 | frame 1\\nframe 2" + Environment.NewLine,
            standardError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenTextServiceThrowsAfterEntryWithoutStackTrace_WritesFinalErrorResultWithNextCursor ()
    {
        var command = new LogsUnityReadCommand(new RecordingLogsUnityService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                CreateEvent(
                    cursor: "stream-1:1",
                    message: "compile warning",
                    source: "compile",
                    stackTrace: null),
                "stream-1:2",
                cancellationToken);
            throw new InvalidOperationException("unity log read failed");
        }), CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(format: "text"));

        Assert.Equal(4, exitCode);
        Assert.Single(standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var commandResult = JsonDocument.Parse(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            commandResult.RootElement,
            UcliCommandNames.LogsUnityRead,
            "error",
            4);
        CommandResultAssert.HasSingleError(commandResult.RootElement, UcliCoreErrorCodes.InternalError);
        var payload = commandResult.RootElement.GetProperty("payload");
        Assert.Equal(1, payload.GetProperty("count").GetInt32());
        Assert.Equal("stream-1:2", payload.GetProperty("nextCursor").GetString());
        Assert.Equal("error", payload.GetProperty("completionReason").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WithTimeoutOption_PassesTimeoutToServiceRequest ()
    {
        var service = new RecordingLogsUnityService(static (_, _, _) =>
        {
            return ValueTask.FromResult(LogsReadServiceResult.Success(count: 0, nextCursor: "stream-1:1"));
        });
        var command = new LogsUnityReadCommand(service, CommandResultTestWriter.Create());

        var (exitCode, _, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(timeout: "1234"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(string.Empty, standardError);
        LogsReadServiceAssert.ReadRequestedWithTimeout(service, 1234);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenFormatIsInvalid_WritesInvalidArgumentResultWithoutCallingService ()
    {
        var service = new RecordingLogsUnityService((_, _, _) => throw new InvalidOperationException("service must not be called"));
        var command = new LogsUnityReadCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(format: "yaml"));

        LogsCommandAssert.UnityReadInvalidArgumentReturnedWithoutExecution(
            exitCode,
            standardOutput,
            standardError,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenDaemonSessionIsNotAvailable_WritesActionRequiredResult ()
    {
        var command = new LogsUnityReadCommand(new RecordingLogsUnityService(static (_, _, _) =>
        {
            return ValueTask.FromResult(LogsReadServiceResult.Failure(ExecutionError.InternalError(
                DaemonSessionNotAvailableMessage,
                DaemonErrorCodes.DaemonSessionNotAvailable)));
        }), CommandResultTestWriter.Create());

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(format: "json"));

        Assert.Equal(4, exitCode);
        Assert.Equal(string.Empty, standardError);
        using var commandResult = JsonDocument.Parse(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            commandResult.RootElement,
            UcliCommandNames.LogsUnityRead,
            "error",
            4);
        CommandResultAssert.HasSingleError(commandResult.RootElement, DaemonErrorCodes.DaemonSessionNotAvailable);
        Assert.Equal(DaemonSessionNotAvailableMessage, commandResult.RootElement.GetProperty("message").GetString());
        var payload = commandResult.RootElement.GetProperty("payload");
        Assert.Equal(0, payload.GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("nextCursor").ValueKind);
        Assert.Equal("error", payload.GetProperty("completionReason").GetString());
        Assert.Equal("startDaemonOrCheckProjectPath", payload.GetProperty("actionRequired").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenCancellationRequested_ReturnsSuccessExitCode ()
    {
        var command = new LogsUnityReadCommand(
            new RecordingLogsUnityService(static (_, _, cancellationToken) => throw new OperationCanceledException(cancellationToken)),
            CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(
            stream: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(4, exitCode);
        Assert.Equal(string.Empty, standardError);
        using var commandResult = JsonDocument.Parse(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            commandResult.RootElement,
            UcliCommandNames.LogsUnityRead,
            "error",
            4);
        CommandResultAssert.HasSingleError(commandResult.RootElement, ExecutionErrorCodes.Canceled);
        var payload = commandResult.RootElement.GetProperty("payload");
        Assert.Equal(0, payload.GetProperty("count").GetInt32());
        Assert.Equal("canceled", payload.GetProperty("completionReason").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("nextCursor").ValueKind);
    }

    private static void AssertSuccessResult (
        string standardOutput,
        int count,
        string nextCursor)
    {
        using var commandResult = JsonDocument.Parse(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            commandResult.RootElement,
            UcliCommandNames.LogsUnityRead,
            "ok",
            0);
        CommandResultAssert.HasNoErrors(commandResult.RootElement);
        var payload = commandResult.RootElement.GetProperty("payload");
        Assert.Equal(count, payload.GetProperty("count").GetInt32());
        Assert.Equal(nextCursor, payload.GetProperty("nextCursor").GetString());
        Assert.Equal("completed", payload.GetProperty("completionReason").GetString());
        Assert.False(payload.TryGetProperty("actionRequired", out _));
    }

    private static void AssertEntryEnvelope (
        JsonElement root,
        int sequence)
    {
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(UcliCommandNames.LogsUnityRead, root.GetProperty("command").GetString());
        Assert.Equal(sequence, root.GetProperty("sequence").GetInt32());
        Assert.Equal("logs.unity.entry", root.GetProperty("event").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
    }

    private static IpcUnityLogEvent CreateEvent (
        string cursor,
        string message,
        string source,
        string? stackTrace)
    {
        return new IpcUnityLogEvent(
            Timestamp: "2026-03-05T10:30:00+09:00",
            Level: "info",
            Source: source,
            Message: message,
            StackTrace: stackTrace,
            Cursor: cursor);
    }

}
