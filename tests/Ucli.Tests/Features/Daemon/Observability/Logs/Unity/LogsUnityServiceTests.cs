using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Daemon;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsUnityServiceTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStreamEnabled_UsesNextCursorForIncrementalReads ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var unityLogsClient = new StubUnityLogsClient(
            [
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("stream-1:1", "alpha"),
                    ],
                    nextCursor: "stream-1:2")),
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("stream-1:2", "bravo"),
                    ],
                    nextCursor: "stream-1:3")),
                UnityLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcUnityLogEvent>(),
                    nextCursor: "stream-1:3")),
            ]);
        var service = CreateService(resolver, unityLogsClient);
        var emittedMessages = new List<string>();

        var result = await service.ExecuteAsync(
            new LogsUnityServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: null,
                After: "stream-1:1",
                Since: "2026-03-06T00:00:00Z",
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: null,
                StackTraceMaxFrames: null,
                StackTraceMaxChars: null,
                Stream: true,
                PollIntervalMilliseconds: 50,
                IdleTimeoutMilliseconds: 1),
            (unityLogEvent, _, _) =>
            {
                emittedMessages.Add(unityLogEvent.Message);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(UcliCommandIds.LogsUnityRead, resolver.LastTimeoutCommand);
        Assert.Equal(["alpha", "bravo"], emittedMessages);
        Assert.Equal(["stream-1:1", "stream-1:2", "stream-1:3"], unityLogsClient.CapturedAfterValues);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTailIsProvidedForStream_ClearsTailAfterInitialRead ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var unityLogsClient = new StubUnityLogsClient(
            [
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("stream-1:100", "alpha"),
                    ],
                    nextCursor: "stream-1:101")),
                UnityLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcUnityLogEvent>(),
                    nextCursor: "stream-1:101")),
            ]);
        var service = CreateService(resolver, unityLogsClient);

        var result = await service.ExecuteAsync(
            new LogsUnityServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: 100,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: null,
                StackTraceMaxFrames: null,
                StackTraceMaxChars: null,
                Stream: true,
                PollIntervalMilliseconds: 50,
                IdleTimeoutMilliseconds: 1),
            static (_, _, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(100, unityLogsClient.CapturedQueries[0].Tail);
        Assert.Null(unityLogsClient.CapturedQueries[1].Tail);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIdleTimeoutIsReached_StopsStreamLoop ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var unityLogsClient = new StubUnityLogsClient(
            [
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("stream-1:10", "first"),
                    ],
                    nextCursor: "stream-1:11")),
                UnityLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcUnityLogEvent>(),
                    nextCursor: "stream-1:11")),
            ]);
        var service = CreateService(resolver, unityLogsClient);

        var result = await service.ExecuteAsync(
            new LogsUnityServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: null,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: null,
                StackTraceMaxFrames: null,
                StackTraceMaxChars: null,
                Stream: true,
                PollIntervalMilliseconds: 50,
                IdleTimeoutMilliseconds: 1),
            static (_, _, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, unityLogsClient.CallCount);
        Assert.Equal(LogsReadCompletionReasons.IdleTimeout, result.CompletionReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUntilTimestampIsReached_ReturnsUntilReachedCompletionReason ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var unityLogsClient = new StubUnityLogsClient(
            [
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("stream-1:10", "first"),
                    ],
                    nextCursor: "stream-1:11")),
                UnityLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcUnityLogEvent>(),
                    nextCursor: "stream-1:11")),
            ]);
        var service = CreateService(resolver, unityLogsClient);

        var result = await service.ExecuteAsync(
            new LogsUnityServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: null,
                After: null,
                Since: null,
                Until: "2026-03-05T10:30:00+09:00",
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: null,
                StackTraceMaxFrames: null,
                StackTraceMaxChars: null,
                Stream: true,
                PollIntervalMilliseconds: 50,
                IdleTimeoutMilliseconds: 3000),
            static (_, _, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, unityLogsClient.CallCount);
        Assert.Equal(LogsReadCompletionReasons.UntilReached, result.CompletionReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCancellationRequested_ThrowsOperationCanceledException ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var service = CreateService(resolver, new StubUnityLogsClient(Array.Empty<UnityLogsClientReadResult>()));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                service.ExecuteAsync(
                        new LogsUnityServiceRequest(
                            ProjectPath: "/tmp/unity-project",
                            Tail: null,
                            After: null,
                            Since: null,
                            Until: null,
                            Level: null,
                            Query: null,
                            QueryTarget: null,
                            Source: null,
                            StackTrace: null,
                            StackTraceMaxFrames: null,
                            StackTraceMaxChars: null,
                            Stream: false,
                            PollIntervalMilliseconds: null,
                            IdleTimeoutMilliseconds: null),
                        static (_, _, _) => ValueTask.CompletedTask,
                        cancellationTokenSource.Token)
                    .AsTask(),
                "Canceled unity logs execution",
                AsyncWaitTimeout);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStackTraceModeIsInvalid_ReturnsInvalidArgument ()
    {
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(
                DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000)));
        var service = CreateService(resolver, new StubUnityLogsClient(Array.Empty<UnityLogsClientReadResult>()));

        var result = await service.ExecuteAsync(
            new LogsUnityServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: null,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: "unsupported",
                StackTraceMaxFrames: null,
                StackTraceMaxChars: null,
                Stream: false,
                PollIntervalMilliseconds: null,
                IdleTimeoutMilliseconds: null),
            static (_, _, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("stackTrace must be one of: none, error, all.", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, resolver.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStackTraceModeIsNone_IgnoresStackTraceLimitValidation ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 3000);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var unityLogsClient = new StubUnityLogsClient(
            [
                UnityLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcUnityLogEvent>(),
                    nextCursor: "stream-1:1")),
            ]);
        var service = CreateService(resolver, unityLogsClient);

        var result = await service.ExecuteAsync(
            new LogsUnityServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: null,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: "none",
                StackTraceMaxFrames: 1024,
                StackTraceMaxChars: 1,
                Stream: false,
                PollIntervalMilliseconds: null,
                IdleTimeoutMilliseconds: null),
            static (_, _, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var query = unityLogsClient.CapturedQueries[0];
        Assert.Equal(IpcUnityLogsStackTraceModeCodec.None, query.StackTrace);
        Assert.Null(query.StackTraceMaxFrames);
        Assert.Null(query.StackTraceMaxChars);
    }

    private static IpcUnityLogEvent CreateEvent (
        string cursor,
        string message)
    {
        return new IpcUnityLogEvent(
            Timestamp: "2026-03-05T10:30:00+09:00",
            Level: "info",
            Source: "runtime",
            Message: message,
            StackTrace: null,
            Cursor: cursor);
    }

    private static IpcUnityLogsReadResponse CreatePayload (
        IpcUnityLogEvent[] events,
        string nextCursor)
    {
        return new IpcUnityLogsReadResponse(events, nextCursor);
    }

    private static LogsUnityService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        IUnityLogsClient unityLogsClient)
    {
        return new LogsUnityService(
            resolver,
            unityLogsClient,
            new LogsUnityRequestValidator(),
            new DaemonLogsStreamTerminationPolicy());
    }

    private sealed class StubUnityLogsClient : IUnityLogsClient
    {
        private readonly Queue<UnityLogsClientReadResult> responses;

        public StubUnityLogsClient (IEnumerable<UnityLogsClientReadResult> responses)
        {
            this.responses = new Queue<UnityLogsClientReadResult>(responses);
        }

        public int CallCount { get; private set; }

        public List<string?> CapturedAfterValues { get; } = new();

        public List<IpcUnityLogsReadRequest> CapturedQueries { get; } = new();

        public ValueTask<UnityLogsClientReadResult> ReadAsync (
            ResolvedUnityProjectContext unityProject,
            IpcUnityLogsReadRequest query,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            CapturedAfterValues.Add(query.After);
            CapturedQueries.Add(query);

            if (responses.Count == 0)
            {
                return ValueTask.FromResult(UnityLogsClientReadResult.Success(
                    new IpcUnityLogsReadResponse(Array.Empty<IpcUnityLogEvent>(), query.After ?? "stream-1:1")));
            }

            return ValueTask.FromResult(responses.Dequeue());
        }
    }
}
