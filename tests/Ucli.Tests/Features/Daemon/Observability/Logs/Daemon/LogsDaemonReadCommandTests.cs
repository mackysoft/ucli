using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Daemon.Logs;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsDaemonReadCommandTests
{
    private const string DaemonSessionNotAvailableMessage = "No daemon session is available for the requested project. Start the daemon or check --projectPath.";

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenFormatIsJson_WritesNdjsonEvents ()
    {
        var command = new LogsDaemonReadCommand(new RecordingLogsDaemonService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                CreateEvent(
                    cursor: "abcdef0123456789abcdef0123456789:1",
                    category: "ipc",
                    message: "server started",
                    raw: null),
                "abcdef0123456789abcdef0123456789:3",
                cancellationToken);
            await onEvent(
                CreateEvent(
                    cursor: "abcdef0123456789abcdef0123456789:2",
                    category: "transport",
                    message: "socket timeout",
                    raw: "{\"socket\":true}"),
                "abcdef0123456789abcdef0123456789:3",
                cancellationToken);
            return LogsReadServiceResult.Completed(count: 2, nextCursor: "abcdef0123456789abcdef0123456789:3");
        }), CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(format: "json"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        AssertSuccessResult(standardOutput, count: 2, nextCursor: "abcdef0123456789abcdef0123456789:3");
        var lines = standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var firstLine = JsonDocument.Parse(lines[0]);
        using var secondLine = JsonDocument.Parse(lines[1]);
        AssertEntryEnvelope(firstLine.RootElement, sequence: 1);
        AssertEntryEnvelope(secondLine.RootElement, sequence: 2);
        var firstPayload = firstLine.RootElement.GetProperty("payload");
        var secondPayload = secondLine.RootElement.GetProperty("payload");
        Assert.Equal("ipc", firstPayload.GetProperty("category").GetString());
        Assert.Equal("server started", firstPayload.GetProperty("message").GetString());
        Assert.Equal("abcdef0123456789abcdef0123456789:1", firstPayload.GetProperty("cursor").GetString());
        Assert.Equal("abcdef0123456789abcdef0123456789:3", firstPayload.GetProperty("nextCursor").GetString());
        Assert.Equal("transport", secondPayload.GetProperty("category").GetString());
        Assert.Equal("{\"socket\":true}", secondPayload.GetProperty("raw").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenFormatIsText_WritesSingleLineEvents ()
    {
        var command = new LogsDaemonReadCommand(new RecordingLogsDaemonService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                CreateEvent(
                    cursor: "abcdef0123456789abcdef0123456789:1",
                    category: "ipc",
                    message: "line 1\nline 2",
                    raw: null),
                "abcdef0123456789abcdef0123456789:2",
                cancellationToken);
            return LogsReadServiceResult.Completed(count: 1, nextCursor: "abcdef0123456789abcdef0123456789:2");
        }), CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(format: "text"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        AssertSuccessResult(standardOutput, count: 1, nextCursor: "abcdef0123456789abcdef0123456789:2");
        Assert.Equal(
            "2026-03-05T10:30:00.0000000+09:00 info ipc line 1\\nline 2" + Environment.NewLine,
            standardError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WithTimeoutOption_PassesTimeoutToServiceRequest ()
    {
        var service = new RecordingLogsDaemonService(static (_, _, _) =>
        {
            return ValueTask.FromResult(LogsReadServiceResult.Completed(count: 0, nextCursor: "abcdef0123456789abcdef0123456789:1"));
        });
        var command = new LogsDaemonReadCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var (exitCode, _, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(timeout: "1234"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(string.Empty, standardError);
        LogsReadServiceAssert.ReadRequestedWithTimeout(service, 1234);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenTimeoutIsInvalid_WritesInvalidArgumentResultWithoutCallingService ()
    {
        var service = new RecordingLogsDaemonService((_, _, _) => throw new InvalidOperationException("service must not be called"));
        var command = new LogsDaemonReadCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(timeout: "0"));

        LogsCommandAssert.DaemonReadInvalidArgumentReturnedWithoutExecution(
            exitCode,
            standardOutput,
            standardError,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenFormatIsInvalid_WritesInvalidArgumentResultWithoutCallingService ()
    {
        var service = new RecordingLogsDaemonService((_, _, _) => throw new InvalidOperationException("service must not be called"));
        var command = new LogsDaemonReadCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(format: "yaml"));

        LogsCommandAssert.DaemonReadInvalidArgumentReturnedWithoutExecution(
            exitCode,
            standardOutput,
            standardError,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenDaemonSessionIsNotAvailable_WritesActionRequiredResult ()
    {
        var command = new LogsDaemonReadCommand(new RecordingLogsDaemonService(static (_, _, _) =>
        {
            return ValueTask.FromResult(LogsReadServiceResult.Failure(
                ExecutionError.InternalError(
                    DaemonSessionNotAvailableMessage,
                    DaemonErrorCodes.DaemonSessionNotAvailable),
                count: 0,
                nextCursor: null));
        }), CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(format: "json"));

        Assert.Equal(4, exitCode);
        Assert.Equal(string.Empty, standardError);
        using var commandResult = JsonDocument.Parse(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            commandResult.RootElement,
            UcliCommandNames.LogsDaemonRead,
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
    public async Task Read_WhenServiceThrowsAfterEntry_WritesFinalErrorResultWithPartialMetadata ()
    {
        var command = new LogsDaemonReadCommand(new RecordingLogsDaemonService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                CreateEvent(
                    cursor: "abcdef0123456789abcdef0123456789:1",
                    category: "ipc",
                    message: "server started",
                    raw: null),
                "abcdef0123456789abcdef0123456789:2",
                cancellationToken);
            throw new InvalidOperationException("read projection failed");
        }), CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(format: "json"));

        Assert.Equal(4, exitCode);
        Assert.Single(standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var commandResult = JsonDocument.Parse(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            commandResult.RootElement,
            UcliCommandNames.LogsDaemonRead,
            "error",
            4);
        CommandResultAssert.HasSingleError(commandResult.RootElement, UcliCoreErrorCodes.InternalError);
        var payload = commandResult.RootElement.GetProperty("payload");
        Assert.Equal(1, payload.GetProperty("count").GetInt32());
        Assert.Equal("abcdef0123456789abcdef0123456789:2", payload.GetProperty("nextCursor").GetString());
        Assert.Equal("error", payload.GetProperty("completionReason").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenCancellationRequestedAfterEntry_WritesFinalCanceledResultWithPartialMetadata ()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var command = new LogsDaemonReadCommand(new RecordingLogsDaemonService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                CreateEvent(
                    cursor: "abcdef0123456789abcdef0123456789:1",
                    category: "ipc",
                    message: "server started",
                    raw: null),
                "abcdef0123456789abcdef0123456789:2",
                cancellationToken);
            await cancellationTokenSource.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return LogsReadServiceResult.Completed(count: 1, nextCursor: "abcdef0123456789abcdef0123456789:2");
        }), CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var (exitCode, standardOutput, standardError) = await StandardOutputCapture.ExecuteWithErrorAsync(() => command.ReadAsync(
            format: "json",
            stream: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(4, exitCode);
        Assert.Single(standardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var commandResult = JsonDocument.Parse(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            commandResult.RootElement,
            UcliCommandNames.LogsDaemonRead,
            "error",
            4);
        CommandResultAssert.HasSingleError(commandResult.RootElement, ExecutionErrorCodes.Canceled);
        var payload = commandResult.RootElement.GetProperty("payload");
        Assert.Equal(1, payload.GetProperty("count").GetInt32());
        Assert.Equal("abcdef0123456789abcdef0123456789:2", payload.GetProperty("nextCursor").GetString());
        Assert.Equal("canceled", payload.GetProperty("completionReason").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenCancellationRequested_ReturnsSuccessExitCode ()
    {
        var command = new LogsDaemonReadCommand(
            new RecordingLogsDaemonService(static (_, _, cancellationToken) => throw new OperationCanceledException(cancellationToken)),
            CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);
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
            UcliCommandNames.LogsDaemonRead,
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
            UcliCommandNames.LogsDaemonRead,
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
        Assert.Equal(UcliCommandNames.LogsDaemonRead, root.GetProperty("command").GetString());
        Assert.Equal(sequence, root.GetProperty("sequence").GetInt32());
        Assert.Equal("logs.daemon.entry", root.GetProperty("event").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
    }

    private static IpcDaemonLogEvent CreateEvent (
        string cursor,
        string category,
        string message,
        string? raw)
    {
        return new IpcDaemonLogEvent(
            Timestamp: new DateTimeOffset(2026, 3, 5, 10, 30, 0, TimeSpan.FromHours(9)),
            Level: IpcLogLevel.Info,
            Category: category,
            Message: message,
            Raw: raw,
            Cursor: new IpcLogCursor(cursor));
    }

}
