using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Daemon.Logs;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsUnityCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Unity_WhenFormatIsJson_WritesNdjsonEvents ()
    {
        var command = new LogsUnityCommand(new StubLogsUnityService(async (_, onEvent, cancellationToken) =>
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
            return LogsDaemonServiceResult.Success();
        }), CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Unity(format: "json"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        var lines = standardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var firstLine = JsonDocument.Parse(lines[0]);
        using var secondLine = JsonDocument.Parse(lines[1]);
        Assert.Equal("compile", firstLine.RootElement.GetProperty("source").GetString());
        Assert.Equal("compile warning", firstLine.RootElement.GetProperty("message").GetString());
        Assert.Equal("stream-1:1", firstLine.RootElement.GetProperty("cursor").GetString());
        Assert.Equal("stream-1:3", firstLine.RootElement.GetProperty("nextCursor").GetString());
        Assert.Equal("runtime", secondLine.RootElement.GetProperty("source").GetString());
        Assert.Equal("at Player.Run()", secondLine.RootElement.GetProperty("stackTrace").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Unity_WhenFormatIsText_WritesSingleLineEventsWithStackTraceSuffix ()
    {
        var command = new LogsUnityCommand(new StubLogsUnityService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                CreateEvent(
                    cursor: "stream-1:1",
                    message: "line 1\nline 2",
                    source: "runtime",
                    stackTrace: "frame 1\nframe 2"),
                "stream-1:2",
                cancellationToken);
            return LogsDaemonServiceResult.Success();
        }), CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Unity(format: "text"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(
            "2026-03-05T10:30:00+09:00 info runtime line 1 line 2 | frame 1 frame 2" + Environment.NewLine,
            standardOutput);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Unity_WhenCancellationRequested_ReturnsSuccessExitCode ()
    {
        var command = new LogsUnityCommand(new ThrowingLogsUnityService(), CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Unity(
            stream: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, standardOutput);
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

    private sealed class StubLogsUnityService : ILogsUnityService
    {
        private readonly Func<LogsUnityServiceRequest, Func<IpcUnityLogEvent, string, CancellationToken, ValueTask>, CancellationToken, ValueTask<LogsDaemonServiceResult>> handler;

        public StubLogsUnityService (Func<LogsUnityServiceRequest, Func<IpcUnityLogEvent, string, CancellationToken, ValueTask>, CancellationToken, ValueTask<LogsDaemonServiceResult>> handler)
        {
            this.handler = handler;
        }

        public ValueTask<LogsDaemonServiceResult> Execute (
            LogsUnityServiceRequest request,
            Func<IpcUnityLogEvent, string, CancellationToken, ValueTask> onEvent,
            CancellationToken cancellationToken = default)
        {
            return handler(request, onEvent, cancellationToken);
        }
    }

    private sealed class ThrowingLogsUnityService : ILogsUnityService
    {
        public ValueTask<LogsDaemonServiceResult> Execute (
            LogsUnityServiceRequest request,
            Func<IpcUnityLogEvent, string, CancellationToken, ValueTask> onEvent,
            CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
