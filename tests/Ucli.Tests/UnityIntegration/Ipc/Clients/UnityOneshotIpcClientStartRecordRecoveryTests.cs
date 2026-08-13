using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityOneshotIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotIpcClientStartRecordRecoveryTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenStartResponseIsLost_RetainsAuthoritativeStartAndRunningProcess ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-oneshot-ipc-client",
            "start-record-response-loss");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var timeProvider = new RecoveryRetryObservationTimeProvider(
            DateTimeOffset.UnixEpoch);
        var executionTimeout = TimeSpan.FromSeconds(5);
        var completionTimeout =
            executionTimeout
            + LifecycleExecutionTiming.ResponseDeliveryGrace;
        var processHandle =
            StubUnityBatchmodeProcessHandle.CreateNonExiting();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var pingAttempt = 0;
        LifecycleExecutionStartBinding? persistedStart = null;
        var startPersisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new RecordingUnityIpcTransportClient(
            async (request, _) =>
            {
                switch (IpcRequestAssert.ParseMethod(request))
                {
                    case UnityIpcMethod.Ping when ++pingAttempt == 1:
                        return CreatePingResponse(request.RequestId);
                    case UnityIpcMethod.Ping:
                        throw new TimeoutException(
                            "Successor oneshot endpoint remained unavailable.");
                    case UnityIpcMethod.LifecycleStart:
                        persistedStart =
                            await LifecycleExecutionIpcTestResponseFactory
                                .PersistStartAsync(unityProject, request);
                        startPersisted.TrySetResult();
                        throw new IpcResponseReadInterruptedException(
                            new EndOfStreamException(
                                "Lifecycle Start response was lost."));
                    default:
                        throw new Xunit.Sdk.XunitException(
                            $"Unexpected method: {request.Method}");
                }
            },
            createLifecycleStartResponses: false);
        var client = CreateClient(
            launcher,
            transportClient,
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(),
            timeProvider: timeProvider);
        var dispatchRequest = CreateCompileDispatchRequest(
            timeProvider,
            executionTimeout);

        var sendTask = client.SendAsync(
                unityProject,
                dispatchRequest,
                ExecutionDeadline.Start(
                    executionTimeout,
                    timeProvider),
                CancellationToken.None)
            .AsTask();
        await startPersisted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(
                timeProvider.RecoveryRetryTimerRegistered,
                sendTask)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(timeProvider.RecoveryRetryTimerRegistered, completedTask);
        Assert.Equal(2, pingAttempt);
        Assert.False(sendTask.IsCompleted);
        timeProvider.Advance(completionTimeout);
        var result = await sendTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            ExecutionErrorCodes.IpcTimeout,
            result.ErrorCode);
        Assert.NotNull(persistedStart);
        Assert.Equal(
            persistedStart!.LifecycleExecutionRef,
            result.LifecycleExecutionStart!.LifecycleExecutionRef);
        Assert.Equal(
            dispatchRequest.Registration!.ExecutionId,
            result.LifecycleExecutionStart.LifecycleExecutionRef.Id);
        Assert.False(result.LifecycleActionDispatched);
        Assert.DoesNotContain(
            transportClient.Requests,
            request => IpcRequestAssert.ParseMethod(request)
                == UnityIpcMethod.Compile);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);
        Assert.Equal(0, processHandle.DisposeCount);
    }

    private sealed class RecoveryRetryObservationTimeProvider : TimeProvider
    {
        private static readonly TimeSpan RecoveryRetryDelay = TimeSpan.FromMilliseconds(50);

        private readonly FakeTimeProvider inner;

        private readonly TaskCompletionSource recoveryRetryTimerRegistered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public RecoveryRetryObservationTimeProvider (DateTimeOffset initialUtc)
        {
            inner = new FakeTimeProvider(initialUtc);
        }

        public Task RecoveryRetryTimerRegistered => recoveryRetryTimerRegistered.Task;

        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        public override long TimestampFrequency => inner.TimestampFrequency;

        public override DateTimeOffset GetUtcNow () => inner.GetUtcNow();

        public override long GetTimestamp () => inner.GetTimestamp();

        public void Advance (TimeSpan elapsed)
        {
            inner.Advance(elapsed);
        }

        public override ITimer CreateTimer (
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = inner.CreateTimer(callback, state, dueTime, period);
            if (dueTime == RecoveryRetryDelay
                && period == Timeout.InfiniteTimeSpan)
            {
                recoveryRetryTimerRegistered.TrySetResult();
            }

            return timer;
        }
    }

}
