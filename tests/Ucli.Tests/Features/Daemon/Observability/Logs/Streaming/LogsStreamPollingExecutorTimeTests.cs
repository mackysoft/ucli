using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsStreamPollingExecutorTimeTests
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPollingContinues_WaitsUsingInjectedTimeProvider ()
    {
        var timeProvider = new ManualTimeProvider();
        var daemonLogsClient = new RecordingDaemonLogsClient([]);
        var executor = new LogsStreamPollingExecutor(CreateResolver(), timeProvider);
        using var cancellationTokenSource = new CancellationTokenSource();

        var resultTask = ExecuteAsync(
            executor,
            daemonLogsClient,
            idleTimeout: null,
            untilTimestamp: null,
            cancellationToken: cancellationTokenSource.Token);

        await timeProvider.WaitForTimerDueWithinAsync(PollInterval).WaitAsync(TestTimeout);
        Assert.Single(daemonLogsClient.Invocations);

        timeProvider.Advance(PollInterval - TimeSpan.FromTicks(1));
        Assert.Single(daemonLogsClient.Invocations);

        timeProvider.Advance(TimeSpan.FromTicks(1));
        await timeProvider.WaitForTimerDueWithinAsync(PollInterval).WaitAsync(TestTimeout);
        Assert.Equal(2, daemonLogsClient.Invocations.Count);

        cancellationTokenSource.Cancel();
        var result = await TestAwaiter.WaitAsync(resultTask, "log stream cancellation", TestTimeout);
        Assert.Equal(LogsReadCompletionReason.Canceled, result.CompletionReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUtcMovesBackward_IdleTimeoutUsesMonotonicElapsedTime ()
    {
        var initialUtc = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(initialUtc);
        var daemonLogsClient = new RecordingDaemonLogsClient([]);
        var executor = new LogsStreamPollingExecutor(CreateResolver(), timeProvider);

        var resultTask = ExecuteAsync(
            executor,
            daemonLogsClient,
            idleTimeout: PollInterval + PollInterval,
            untilTimestamp: null,
            cancellationToken: CancellationToken.None);

        await timeProvider.WaitForTimerDueWithinAsync(PollInterval).WaitAsync(TestTimeout);
        timeProvider.ShiftUtc(-TimeSpan.FromDays(1));

        timeProvider.Advance(PollInterval);
        await timeProvider.WaitForTimerDueWithinAsync(PollInterval).WaitAsync(TestTimeout);
        Assert.False(resultTask.IsCompleted);

        timeProvider.Advance(PollInterval);
        var result = await TestAwaiter.WaitAsync(resultTask, "idle log stream completion", TestTimeout);

        Assert.Equal(LogsReadCompletionReason.IdleTimeout, result.CompletionReason);
        Assert.Equal(3, daemonLogsClient.Invocations.Count);
        Assert.True(timeProvider.GetUtcNow() < initialUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUtcReachesUntil_StopsWithoutWaitingForEquivalentMonotonicDuration ()
    {
        var initialUtc = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
        var untilTimestamp = initialUtc + TimeSpan.FromHours(1);
        var timeProvider = new ManualTimeProvider(initialUtc);
        var startedAtTimestamp = timeProvider.GetTimestamp();
        var daemonLogsClient = new RecordingDaemonLogsClient([]);
        var executor = new LogsStreamPollingExecutor(CreateResolver(), timeProvider);

        var resultTask = ExecuteAsync(
            executor,
            daemonLogsClient,
            idleTimeout: null,
            untilTimestamp: untilTimestamp,
            cancellationToken: CancellationToken.None);

        await timeProvider.WaitForTimerDueWithinAsync(PollInterval).WaitAsync(TestTimeout);
        timeProvider.ShiftUtc(TimeSpan.FromHours(2));
        timeProvider.Advance(PollInterval);

        var result = await TestAwaiter.WaitAsync(resultTask, "until-bounded log stream completion", TestTimeout);

        Assert.Equal(LogsReadCompletionReason.UntilReached, result.CompletionReason);
        Assert.Equal(2, daemonLogsClient.Invocations.Count);
        Assert.Equal(PollInterval, timeProvider.GetElapsedTime(startedAtTimestamp));
    }

    private static RecordingDaemonCommandExecutionContextResolver CreateResolver ()
    {
        return new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(
                DaemonCommandExecutionContextTestFactory.Create(
                    timeoutMilliseconds: 3000,
                    unityVersion: ProjectIdentityDefaults.UnknownUnityVersion)));
    }

    private static Task<LogsReadServiceResult> ExecuteAsync (
        LogsStreamPollingExecutor executor,
        IDaemonLogsClient daemonLogsClient,
        TimeSpan? idleTimeout,
        DateTimeOffset? untilTimestamp,
        CancellationToken cancellationToken)
    {
        return executor.ExecuteAsync(
                UcliCommandIds.LogsDaemonRead,
                projectPath: null,
                timeoutMilliseconds: null,
                new IpcDaemonLogsReadRequest(
                    Tail: null,
                    After: null,
                    Since: null,
                    Until: null,
                    Level: null,
                    Query: null,
                    QueryTarget: null,
                    Category: null),
                stream: true,
                new LogsStreamRuntimeOptions(PollInterval, idleTimeout, untilTimestamp),
                daemonLogsClient.ReadAsync,
                static result => result.Response,
                static result => result.Error,
                static (query, after) => new IpcDaemonLogsReadRequest(
                    Tail: null,
                    After: after,
                    Since: query.Since,
                    Until: query.Until,
                    Level: query.Level,
                    Query: query.Query,
                    QueryTarget: query.QueryTarget,
                    Category: query.Category),
                static response => response.Events,
                static response => response.NextCursor.Value,
                static logEvent => logEvent.Cursor.Value,
                static (_, _, _) => ValueTask.CompletedTask,
                static logEvent => logEvent.Timestamp,
                cancellationToken)
            .AsTask();
    }
}
