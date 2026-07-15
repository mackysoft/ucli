using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsDaemonServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCategoryIsAll_SendsNoCategoryFilter ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 3000, unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonLogsClient = new RecordingDaemonLogsClient(
            [
                DaemonLogsClientReadResult.Success(CreatePayload([], "abcdef0123456789abcdef0123456789:1")),
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
                Category: " ALL ",
                Stream: false,
                PollIntervalMilliseconds: null,
                IdleTimeoutMilliseconds: null),
            static (_, _, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        DaemonLogsClientAssert.SingleReadHasNoCategoryFilter(daemonLogsClient);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStreamEnabled_UsesNextCursorForIncrementalReads ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 3000, unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonLogsClient = new RecordingDaemonLogsClient(
            [
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("abcdef0123456789abcdef0123456789:1", "alpha"),
                    ],
                    nextCursor: "abcdef0123456789abcdef0123456789:2")),
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("abcdef0123456789abcdef0123456789:2", "bravo"),
                    ],
                    nextCursor: "abcdef0123456789abcdef0123456789:3")),
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcDaemonLogEvent>(),
                    nextCursor: "abcdef0123456789abcdef0123456789:3")),
            ]);
        var service = CreateImmediateIdleStreamService(resolver, daemonLogsClient);
        var emittedMessages = new List<string>();

        var result = await service.ExecuteAsync(
            new LogsDaemonServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: null,
                After: "abcdef0123456789abcdef0123456789:1",
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Category: null,
                Stream: true,
                PollIntervalMilliseconds: 50,
                IdleTimeoutMilliseconds: 1)
            {
                TimeoutMilliseconds = 4321,
            },
            (daemonLogEvent, _, _) =>
            {
                emittedMessages.Add(daemonLogEvent.Message);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        DaemonCommandExecutionContextResolverAssert.ResolvedFor(
            resolver,
            UcliCommandIds.LogsDaemonRead,
            "/tmp/unity-project",
            4321);
        Assert.Equal(["alpha", "bravo"], emittedMessages);
        DaemonLogsClientAssert.ReadAfterCursors(
            daemonLogsClient,
            "abcdef0123456789abcdef0123456789:1",
            "abcdef0123456789abcdef0123456789:2",
            "abcdef0123456789abcdef0123456789:3");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCanceledDuringBatch_ReturnsLastEmittedEventCursor ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 3000, unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonLogsClient = new RecordingDaemonLogsClient(
            [
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("abcdef0123456789abcdef0123456789:1", "alpha"),
                        CreateEvent("abcdef0123456789abcdef0123456789:2", "bravo"),
                    ],
                    nextCursor: "abcdef0123456789abcdef0123456789:3")),
            ]);
        var service = CreateImmediateIdleStreamService(resolver, daemonLogsClient);
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
        Assert.Equal("abcdef0123456789abcdef0123456789:1", result.NextCursor);
        Assert.Equal(LogsReadCompletionReason.Canceled, result.CompletionReason);
        Assert.Equal(["alpha"], emittedMessages);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTailIsProvidedForStream_ClearsTailAfterInitialRead ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 3000, unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonLogsClient = new RecordingDaemonLogsClient(
            [
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("abcdef0123456789abcdef0123456789:100", "alpha"),
                    ],
                    nextCursor: "abcdef0123456789abcdef0123456789:101")),
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcDaemonLogEvent>(),
                    nextCursor: "abcdef0123456789abcdef0123456789:101")),
            ]);
        var service = CreateImmediateIdleStreamService(resolver, daemonLogsClient);

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
        DaemonLogsClientAssert.ReadTailValues(daemonLogsClient, 100, null);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIdleTimeoutIsReached_StopsStreamLoop ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 3000, unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonLogsClient = new RecordingDaemonLogsClient(
            [
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events:
                    [
                        CreateEvent("abcdef0123456789abcdef0123456789:10", "first"),
                    ],
                    nextCursor: "abcdef0123456789abcdef0123456789:11")),
                DaemonLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcDaemonLogEvent>(),
                    nextCursor: "abcdef0123456789abcdef0123456789:11")),
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
        DaemonLogsClientAssert.ReadAfterCursors(daemonLogsClient, null, "abcdef0123456789abcdef0123456789:11");
        Assert.Equal(LogsReadCompletionReason.IdleTimeout, result.CompletionReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUntilTimestampIsReached_ReturnsUntilReachedCompletionReason ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 3000, unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonLogsClient = new RecordingDaemonLogsClient(
        [
            DaemonLogsClientReadResult.Success(CreatePayload(
                events:
                [
                    CreateEvent("abcdef0123456789abcdef0123456789:10", "first"),
                ],
                nextCursor: "abcdef0123456789abcdef0123456789:11")),
        ]);
        var service = CreateService(resolver, daemonLogsClient);

        var result = await service.ExecuteAsync(
            new LogsDaemonServiceRequest(
                ProjectPath: "/tmp/unity-project",
                Tail: null,
                After: null,
                Since: null,
                Until: "2026-03-05T10:30:00+09:00",
                Level: null,
                Query: null,
                QueryTarget: null,
                Category: null,
                Stream: true,
                PollIntervalMilliseconds: 50,
                IdleTimeoutMilliseconds: null),
            static (_, _, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        DaemonLogsClientAssert.SingleReadWithoutAfterCursor(daemonLogsClient);
        Assert.Equal(LogsReadCompletionReason.UntilReached, result.CompletionReason);
    }

    private static IpcDaemonLogEvent CreateEvent (
        string cursor,
        string message)
    {
        return new IpcDaemonLogEvent(
            Timestamp: new DateTimeOffset(2026, 3, 5, 10, 30, 0, TimeSpan.FromHours(9)),
            Level: IpcLogLevel.Info,
            Category: "ipc",
            Message: message,
            Raw: null,
            Cursor: new IpcLogCursor(cursor));
    }

    private static IpcDaemonLogsReadResponse CreatePayload (
        IpcDaemonLogEvent[] events,
        string nextCursor)
    {
        return new IpcDaemonLogsReadResponse(events, new IpcLogCursor(nextCursor));
    }

    private static LogsDaemonService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        IDaemonLogsClient daemonLogsClient,
        ILogsDaemonRequestValidator? requestValidator = null)
    {
        return new LogsDaemonService(
            new LogsStreamPollingExecutor(resolver, TimeProvider.System),
            daemonLogsClient,
            requestValidator ?? new LogsDaemonRequestValidator());
    }

    private static LogsDaemonService CreateImmediateIdleStreamService (
        IDaemonCommandExecutionContextResolver resolver,
        IDaemonLogsClient daemonLogsClient)
    {
        return CreateService(
            resolver,
            daemonLogsClient,
            LogsStreamRequestValidatorTestAdapters.CreateDaemonZeroPollInterval(TimeSpan.Zero));
    }

}
