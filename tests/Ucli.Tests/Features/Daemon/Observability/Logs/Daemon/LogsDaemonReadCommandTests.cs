using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Daemon.Logs;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsDaemonReadCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenFormatIsJson_WritesNdjsonEvents ()
    {
        var command = new LogsDaemonReadCommand(new StubLogsDaemonService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                CreateEvent(
                    cursor: "stream-1:1",
                    category: "ipc",
                    message: "server started",
                    raw: null),
                "stream-1:3",
                cancellationToken);
            await onEvent(
                CreateEvent(
                    cursor: "stream-1:2",
                    category: "transport",
                    message: "socket timeout",
                    raw: "{\"socket\":true}"),
                "stream-1:3",
                cancellationToken);
            return LogsReadServiceResult.Success();
        }), CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ReadAsync(format: "json"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        var lines = standardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var firstLine = JsonDocument.Parse(lines[0]);
        using var secondLine = JsonDocument.Parse(lines[1]);
        Assert.Equal("ipc", firstLine.RootElement.GetProperty("category").GetString());
        Assert.Equal("server started", firstLine.RootElement.GetProperty("message").GetString());
        Assert.Equal("stream-1:1", firstLine.RootElement.GetProperty("cursor").GetString());
        Assert.Equal("stream-1:3", firstLine.RootElement.GetProperty("nextCursor").GetString());
        Assert.Equal("transport", secondLine.RootElement.GetProperty("category").GetString());
        Assert.Equal("{\"socket\":true}", secondLine.RootElement.GetProperty("raw").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenFormatIsText_WritesSingleLineEvents ()
    {
        var command = new LogsDaemonReadCommand(new StubLogsDaemonService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                CreateEvent(
                    cursor: "stream-1:1",
                    category: "ipc",
                    message: "line 1\nline 2",
                    raw: null),
                "stream-1:2",
                cancellationToken);
            return LogsReadServiceResult.Success();
        }), CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ReadAsync(format: "text"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(
            "2026-03-05T10:30:00+09:00 info ipc line 1 line 2" + Environment.NewLine,
            standardOutput);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenCancellationRequested_ReturnsSuccessExitCode ()
    {
        var command = new LogsDaemonReadCommand(new ThrowingLogsDaemonService(), CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ReadAsync(
            stream: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, standardOutput);
    }

    private static IpcDaemonLogEvent CreateEvent (
        string cursor,
        string category,
        string message,
        string? raw)
    {
        return new IpcDaemonLogEvent(
            Timestamp: "2026-03-05T10:30:00+09:00",
            Level: "info",
            Category: category,
            Message: message,
            Raw: raw,
            Cursor: cursor);
    }

    private sealed class StubLogsDaemonService : ILogsDaemonService
    {
        private readonly Func<LogsDaemonServiceRequest, Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask>, CancellationToken, ValueTask<LogsReadServiceResult>> handler;

        public StubLogsDaemonService (Func<LogsDaemonServiceRequest, Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask>, CancellationToken, ValueTask<LogsReadServiceResult>> handler)
        {
            this.handler = handler;
        }

        public ValueTask<LogsReadServiceResult> ExecuteAsync (
            LogsDaemonServiceRequest request,
            Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask> onEvent,
            CancellationToken cancellationToken = default)
        {
            return handler(request, onEvent, cancellationToken);
        }
    }

    private sealed class ThrowingLogsDaemonService : ILogsDaemonService
    {
        public ValueTask<LogsReadServiceResult> ExecuteAsync (
            LogsDaemonServiceRequest request,
            Func<IpcDaemonLogEvent, string, CancellationToken, ValueTask> onEvent,
            CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
