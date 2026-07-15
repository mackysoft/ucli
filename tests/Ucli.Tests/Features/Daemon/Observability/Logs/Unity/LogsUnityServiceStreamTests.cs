using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Logs.LogsUnityServiceTestSupport;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsUnityServiceStreamTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStreamReadTimesOutBeforeIdleTimeout_RetriesPolling ()
    {
        var resolver = CreateResolver();
        var unityLogsClient = new RecordingUnityLogsClient(
            [
                UnityLogsClientReadResult.Failure(ExecutionError.Timeout("Unity logs read request timed out.")),
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent(
                            "stream-1:1",
                            "alpha",
                            new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                    ],
                    nextCursor: "stream-1:2")),
            ]);
        var service = CreateZeroPollIntervalService(resolver, unityLogsClient);
        var emittedMessages = new List<string>();

        var result = await service.ExecuteAsync(
            new LogsUnityServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: null,
                After: null,
                Since: null,
                Until: "2099-01-01T00:00:00Z",
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: null,
                StackTraceMaxFrames: null,
                StackTraceMaxChars: null,
                Stream: true,
                PollIntervalMilliseconds: 50,
                IdleTimeoutMilliseconds: 15000),
            (unityLogEvent, _, _) =>
            {
                emittedMessages.Add(unityLogEvent.Message);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(["alpha"], emittedMessages);
        UnityLogsClientAssert.ReadTimeouts(
            unityLogsClient,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStreamEnabled_UsesNextCursorForIncrementalReads ()
    {
        var resolver = CreateResolver();
        var unityLogsClient = new RecordingUnityLogsClient(
            [
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent(
                            "stream-1:1",
                            "alpha",
                            new DateTimeOffset(2026, 3, 5, 10, 30, 0, TimeSpan.FromHours(9))),
                    ],
                    nextCursor: "stream-1:2")),
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent(
                            "stream-1:2",
                            "bravo",
                            new DateTimeOffset(2026, 3, 5, 10, 30, 0, TimeSpan.FromHours(9))),
                    ],
                    nextCursor: "stream-1:3")),
                UnityLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcUnityLogEvent>(),
                    nextCursor: "stream-1:3")),
            ]);
        var service = CreateImmediateIdleStreamService(resolver, unityLogsClient);
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
                IdleTimeoutMilliseconds: 1)
            {
                TimeoutMilliseconds = 4321,
            },
            (unityLogEvent, _, _) =>
            {
                emittedMessages.Add(unityLogEvent.Message);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        DaemonCommandExecutionContextResolverAssert.ResolvedFor(
            resolver,
            UcliCommandIds.LogsUnityRead,
            "/tmp/unity-project",
            4321);
        Assert.Equal(["alpha", "bravo"], emittedMessages);
        UnityLogsClientAssert.ReadAfterCursors(
            unityLogsClient,
            "stream-1:1",
            "stream-1:2",
            "stream-1:3");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTailIsProvidedForStream_ClearsTailAfterInitialRead ()
    {
        var resolver = CreateResolver();
        var unityLogsClient = new RecordingUnityLogsClient(
            [
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent(
                            "stream-1:100",
                            "alpha",
                            new DateTimeOffset(2026, 3, 5, 10, 30, 0, TimeSpan.FromHours(9))),
                    ],
                    nextCursor: "stream-1:101")),
                UnityLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcUnityLogEvent>(),
                    nextCursor: "stream-1:101")),
            ]);
        var service = CreateImmediateIdleStreamService(resolver, unityLogsClient);

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
        UnityLogsClientAssert.ReadTailValues(unityLogsClient, 100, null);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIdleTimeoutIsReached_StopsStreamLoop ()
    {
        var resolver = CreateResolver();
        var unityLogsClient = new RecordingUnityLogsClient(
            [
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent(
                            "stream-1:10",
                            "first",
                            new DateTimeOffset(2026, 3, 5, 10, 30, 0, TimeSpan.FromHours(9))),
                    ],
                    nextCursor: "stream-1:11")),
                UnityLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcUnityLogEvent>(),
                    nextCursor: "stream-1:11")),
            ]);
        var service = CreateImmediateIdleStreamService(resolver, unityLogsClient);

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
        UnityLogsClientAssert.ReadAfterCursors(unityLogsClient, null, "stream-1:11");
        Assert.Equal(LogsReadCompletionReason.IdleTimeout, result.CompletionReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStreamReadTimesOutAfterIdleTimeout_ReturnsIdleTimeout ()
    {
        var resolver = CreateResolver();
        var unityLogsClient = new RecordingUnityLogsClient(
            [
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent(
                            "stream-1:10",
                            "first",
                            new DateTimeOffset(2026, 3, 5, 10, 30, 0, TimeSpan.FromHours(9))),
                    ],
                    nextCursor: "stream-1:11")),
                UnityLogsClientReadResult.Failure(ExecutionError.Timeout("Unity logs read request timed out.")),
            ]);
        var service = CreateImmediateIdleStreamService(resolver, unityLogsClient);

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
        Assert.Equal(1, result.Count);
        Assert.Equal("stream-1:11", result.NextCursor);
        UnityLogsClientAssert.ReadAfterCursors(unityLogsClient, null, "stream-1:11");
        Assert.Equal(LogsReadCompletionReason.IdleTimeout, result.CompletionReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUntilTimestampIsReached_ReturnsUntilReachedCompletionReason ()
    {
        var resolver = CreateResolver();
        var unityLogsClient = new RecordingUnityLogsClient(
            [
                UnityLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent(
                            "stream-1:10",
                            "first",
                            new DateTimeOffset(2026, 3, 5, 10, 30, 0, TimeSpan.FromHours(9))),
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
        UnityLogsClientAssert.SingleReadWithoutAfterCursor(unityLogsClient);
        Assert.Equal(LogsReadCompletionReason.UntilReached, result.CompletionReason);
    }
}
