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

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPollingContinues_WaitsUsingInjectedTimeProvider ()
    {
        var innerTimeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var timeProvider = new PollDelayObservingTimeProvider(innerTimeProvider, PollInterval);
        var daemonLogsClient = new RecordingDaemonLogsClient([]);
        var executor = new LogsStreamPollingExecutor(CreateResolver(), timeProvider);
        using var cancellationTokenSource = new CancellationTokenSource();

        var resultTask = ExecuteAsync(
            executor,
            daemonLogsClient,
            idleTimeout: null,
            untilTimestamp: null,
            cancellationToken: cancellationTokenSource.Token);

        await daemonLogsClient.WaitForNextReadAsync();
        await timeProvider.WaitForNextPollDelayRegistrationAsync();
        Assert.Single(daemonLogsClient.Invocations);

        innerTimeProvider.Advance(PollInterval - TimeSpan.FromTicks(1));
        Assert.Single(daemonLogsClient.Invocations);

        innerTimeProvider.Advance(TimeSpan.FromTicks(1));
        await daemonLogsClient.WaitForNextReadAsync();
        Assert.Equal(2, daemonLogsClient.Invocations.Count);

        cancellationTokenSource.Cancel();
        var result = await resultTask;
        Assert.Equal(LogsReadCompletionReason.Canceled, result.CompletionReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUtcMovesBackward_IdleTimeoutUsesMonotonicElapsedTime ()
    {
        var initialUtc = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
        var innerTimeProvider = new WallClockSkewFakeTimeProvider(initialUtc);
        var timeProvider = new PollDelayObservingTimeProvider(innerTimeProvider, PollInterval);
        var daemonLogsClient = new RecordingDaemonLogsClient([]);
        var executor = new LogsStreamPollingExecutor(CreateResolver(), timeProvider);

        var resultTask = ExecuteAsync(
            executor,
            daemonLogsClient,
            idleTimeout: PollInterval + PollInterval,
            untilTimestamp: null,
            cancellationToken: CancellationToken.None);

        await daemonLogsClient.WaitForNextReadAsync();
        await timeProvider.WaitForNextPollDelayRegistrationAsync();
        innerTimeProvider.ShiftUtc(-TimeSpan.FromDays(1));

        innerTimeProvider.Advance(PollInterval);
        await daemonLogsClient.WaitForNextReadAsync();
        await timeProvider.WaitForNextPollDelayRegistrationAsync();
        Assert.False(resultTask.IsCompleted);

        innerTimeProvider.Advance(PollInterval);
        await daemonLogsClient.WaitForNextReadAsync();
        var result = await resultTask;

        Assert.Equal(LogsReadCompletionReason.IdleTimeout, result.CompletionReason);
        Assert.Equal(3, daemonLogsClient.Invocations.Count);
        Assert.True(innerTimeProvider.GetUtcNow() < initialUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUtcReachesUntil_StopsWithoutWaitingForEquivalentMonotonicDuration ()
    {
        var initialUtc = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
        var untilTimestamp = initialUtc + TimeSpan.FromHours(1);
        var innerTimeProvider = new WallClockSkewFakeTimeProvider(initialUtc);
        var timeProvider = new PollDelayObservingTimeProvider(innerTimeProvider, PollInterval);
        var startedAtTimestamp = innerTimeProvider.GetTimestamp();
        var daemonLogsClient = new RecordingDaemonLogsClient([]);
        var executor = new LogsStreamPollingExecutor(CreateResolver(), timeProvider);

        var resultTask = ExecuteAsync(
            executor,
            daemonLogsClient,
            idleTimeout: null,
            untilTimestamp: untilTimestamp,
            cancellationToken: CancellationToken.None);

        await daemonLogsClient.WaitForNextReadAsync();
        await timeProvider.WaitForNextPollDelayRegistrationAsync();
        innerTimeProvider.ShiftUtc(TimeSpan.FromHours(2));
        innerTimeProvider.Advance(PollInterval);

        await daemonLogsClient.WaitForNextReadAsync();
        var result = await resultTask;

        Assert.Equal(LogsReadCompletionReason.UntilReached, result.CompletionReason);
        Assert.Equal(2, daemonLogsClient.Invocations.Count);
        Assert.Equal(PollInterval, innerTimeProvider.GetElapsedTime(startedAtTimestamp));
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

    private sealed class PollDelayObservingTimeProvider : TimeProvider
    {
        private readonly TimeProvider inner;

        private readonly TimeSpan pollInterval;

        private readonly SemaphoreSlim pollDelaySignals = new(0);

        public PollDelayObservingTimeProvider (TimeProvider inner, TimeSpan pollInterval)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.pollInterval = pollInterval;
        }

        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override DateTimeOffset GetUtcNow ()
        {
            return inner.GetUtcNow();
        }

        public override long GetTimestamp ()
        {
            return inner.GetTimestamp();
        }

        public override ITimer CreateTimer (
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = inner.CreateTimer(callback, state, dueTime, period);
            if (dueTime == pollInterval && period == Timeout.InfiniteTimeSpan)
            {
                pollDelaySignals.Release();
            }

            return timer;
        }

        public Task WaitForNextPollDelayRegistrationAsync ()
        {
            return pollDelaySignals.WaitAsync();
        }
    }
}
