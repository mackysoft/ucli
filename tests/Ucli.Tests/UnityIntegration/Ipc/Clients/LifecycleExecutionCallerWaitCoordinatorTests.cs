using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class LifecycleExecutionCallerWaitCoordinatorTests
{
    private static readonly TimeSpan SignalWaitTimeout =
        TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitAsync_WhenCallerCancelsAfterObservedActionDispatch_ReturnsStartAndLeavesDispatchRunning ()
    {
        using var callerCancellation = new CancellationTokenSource();
        var unityProject = ResolvedUnityProjectContextTestFactory.Create();
        var dispatchRequest = CreateLifecycleDispatchRequest(
            LifecycleExecutionKind.Compile);
        var expectedStart =
            LifecycleExecutionIpcTestResponseFactory.CreateStartBinding(
                dispatchRequest.CreateLifecycleStartRequest());
        var dispatchRelease = new TaskCompletionSource<UnityRequestExecutionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await LifecycleExecutionCallerWaitCoordinator.WaitAsync(
                unityProject,
                dispatchRequest,
                ExecutionDeadline.Start(
                    TimeSpan.FromSeconds(30),
                    TimeProvider.System),
                observation =>
                {
                    Assert.NotNull(observation);
                    observation!.ReportStarted(expectedStart);
                    observation.ReportActionDispatched();
                    callerCancellation.Cancel();
                    return new ValueTask<UnityRequestExecutionResult>(
                        dispatchRelease.Task);
                },
                callerCancellation.Token)
            .AsTask()
            .WaitAsync(SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.ErrorCode);
        Assert.Same(expectedStart, result.LifecycleExecutionStart);
        Assert.True(result.LifecycleActionDispatched);
        Assert.False(dispatchRelease.Task.IsCompleted);

        dispatchRelease.TrySetResult(CreateReleasedDispatchResult());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WaitAsync_WhenStartPersistsAfterExecutionDeadline_UsesDeliveryGraceWithoutStoppingDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-caller-wait",
            "persisted-start");
        using var callerCancellation = new CancellationTokenSource();
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var timeProvider = new ManualTimeProvider();
        var executionTimeout = TimeSpan.FromSeconds(30);
        var dispatchRequest = CreateLifecycleDispatchRequest(
            LifecycleExecutionKind.Compile,
            timeProvider,
            executionTimeout);
        var dispatchRelease = new TaskCompletionSource<UnityRequestExecutionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var resultTask = LifecycleExecutionCallerWaitCoordinator.WaitAsync(
                unityProject,
                dispatchRequest,
                ExecutionDeadline.Start(
                    executionTimeout,
                    timeProvider),
                observation =>
                {
                    Assert.NotNull(observation);
                    callerCancellation.Cancel();
                    return new ValueTask<UnityRequestExecutionResult>(
                        dispatchRelease.Task);
                },
                callerCancellation.Token)
            .AsTask();
        await timeProvider.WaitForTimerDueWithinAsync(
            TimeSpan.FromMilliseconds(10));
        timeProvider.Advance(
            executionTimeout + TimeSpan.FromSeconds(1));
        var persistedStart =
            await LifecycleExecutionIpcTestResponseFactory.PersistStartAsync(
                unityProject,
                dispatchRequest);
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            LifecycleExecutionTiming.ResponseDeliveryGrace,
            TimeSpan.FromMilliseconds(10));
        var result = await resultTask.WaitAsync(SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.ErrorCode);
        Assert.Equal(
            persistedStart.LifecycleExecutionRef,
            result.LifecycleExecutionStart!.LifecycleExecutionRef);
        Assert.False(result.LifecycleActionDispatched);
        Assert.False(dispatchRelease.Task.IsCompleted);

        dispatchRelease.TrySetResult(CreateReleasedDispatchResult());
    }

    private static UnityRequestExecutionResult CreateReleasedDispatchResult ()
    {
        return UnityRequestExecutionResult.Failure(
            UnityIpcFailureClassifier.InternalError(
                "The test dispatch was released after caller-wait assertions."));
    }
}
