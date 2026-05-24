using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Daemon;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsDaemonServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStreamEnabled_UsesNextCursorForIncrementalReads ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonLogsClient = new StubDaemonLogsClient(
            [
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("stream-1:1", "alpha"),
                    ],
                    nextCursor: "stream-1:2")),
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("stream-1:2", "bravo"),
                    ],
                    nextCursor: "stream-1:3")),
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcDaemonLogEvent>(),
                    nextCursor: "stream-1:3")),
            ]);
        var service = CreateService(resolver, daemonLogsClient);
        var emittedMessages = new List<string>();

        var result = await service.ExecuteAsync(
            new LogsDaemonServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: null,
                After: "stream-1:1",
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Category: null,
                Stream: true,
                PollIntervalMilliseconds: 50,
                IdleTimeoutMilliseconds: 1),
            (daemonLogEvent, _, _) =>
            {
                emittedMessages.Add(daemonLogEvent.Message);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(UcliCommandIds.LogsDaemonRead, resolver.LastTimeoutCommand);
        Assert.Equal(["alpha", "bravo"], emittedMessages);
        Assert.Equal(["stream-1:1", "stream-1:2", "stream-1:3"], daemonLogsClient.CapturedAfterValues);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCanceledDuringBatch_ReturnsLastEmittedEventCursor ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonLogsClient = new StubDaemonLogsClient(
            [
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("stream-1:1", "alpha"),
                        CreateEvent("stream-1:2", "bravo"),
                    ],
                    nextCursor: "stream-1:3")),
            ]);
        var service = CreateService(resolver, daemonLogsClient);
        var emittedMessages = new List<string>();
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await service.ExecuteAsync(
            new LogsDaemonServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: null,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Category: null,
                Stream: true,
                PollIntervalMilliseconds: 50,
                IdleTimeoutMilliseconds: null),
            (daemonLogEvent, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                emittedMessages.Add(daemonLogEvent.Message);
                if (emittedMessages.Count == 1)
                {
                    cancellationTokenSource.Cancel();
                }

                return ValueTask.CompletedTask;
            },
            cancellationTokenSource.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.Error!.Code);
        Assert.Equal(1, result.Count);
        Assert.Equal("stream-1:1", result.NextCursor);
        Assert.Equal("canceled", result.CompletionReason);
        Assert.Equal(["alpha"], emittedMessages);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTailIsProvidedForStream_ClearsTailAfterInitialRead ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonLogsClient = new StubDaemonLogsClient(
            [
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("stream-1:100", "alpha"),
                    ],
                    nextCursor: "stream-1:101")),
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcDaemonLogEvent>(),
                    nextCursor: "stream-1:101")),
            ]);
        var service = CreateService(resolver, daemonLogsClient);

        var result = await service.ExecuteAsync(
            new LogsDaemonServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: 100,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Category: null,
                Stream: true,
                PollIntervalMilliseconds: 50,
                IdleTimeoutMilliseconds: 1),
            static (_, _, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(100, daemonLogsClient.CapturedQueries[0].Tail);
        Assert.Null(daemonLogsClient.CapturedQueries[1].Tail);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIdleTimeoutIsReached_StopsStreamLoop ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonLogsClient = new StubDaemonLogsClient(
            [
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("stream-1:10", "first"),
                    ],
                    nextCursor: "stream-1:11")),
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcDaemonLogEvent>(),
                    nextCursor: "stream-1:11")),
            ]);
        var service = CreateService(resolver, daemonLogsClient);

        var result = await service.ExecuteAsync(
            new LogsDaemonServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: null,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Category: null,
                Stream: true,
                PollIntervalMilliseconds: 50,
                IdleTimeoutMilliseconds: 1),
            static (_, _, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, daemonLogsClient.CallCount);
    }

    private static IpcDaemonLogEvent CreateEvent (
        string cursor,
        string message)
    {
        return new IpcDaemonLogEvent(
            Timestamp: "2026-03-05T10:30:00+09:00",
            Level: "info",
            Category: "ipc",
            Message: message,
            Raw: null,
            Cursor: cursor);
    }

    private static IpcDaemonLogsReadResponse CreatePayload (
        IpcDaemonLogEvent[] events,
        string nextCursor)
    {
        return new IpcDaemonLogsReadResponse(events, nextCursor);
    }

    private static LogsDaemonService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        IDaemonLogsClient daemonLogsClient)
    {
        return new LogsDaemonService(
            resolver,
            daemonLogsClient,
            new LogsDaemonRequestValidator(),
            new DaemonLogsStreamTerminationPolicy());
    }

    private sealed class StubDaemonLogsClient : IDaemonLogsClient
    {
        private readonly Queue<DaemonLogsClientReadResult> responses;

        public StubDaemonLogsClient (IEnumerable<DaemonLogsClientReadResult> responses)
        {
            this.responses = new Queue<DaemonLogsClientReadResult>(responses);
        }

        public int CallCount { get; private set; }

        public List<string?> CapturedAfterValues { get; } = new();

        public List<IpcDaemonLogsReadRequest> CapturedQueries { get; } = new();

        public ValueTask<DaemonLogsClientReadResult> ReadAsync (
            ResolvedUnityProjectContext unityProject,
            IpcDaemonLogsReadRequest query,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            CapturedAfterValues.Add(query.After);
            CapturedQueries.Add(query);

            if (responses.Count == 0)
            {
                return ValueTask.FromResult(DaemonLogsClientReadResult.Success(
                    new IpcDaemonLogsReadResponse(Array.Empty<IpcDaemonLogEvent>(), query.After ?? "stream-1:1")));
            }

            return ValueTask.FromResult(responses.Dequeue());
        }
    }

}
