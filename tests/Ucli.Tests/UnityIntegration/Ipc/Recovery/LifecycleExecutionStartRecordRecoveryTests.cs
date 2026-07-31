using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class LifecycleExecutionStartRecordRecoveryTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task WaitUntilAvailableAsync_WhenInitialReadMissesThenStartAppears_ReturnsMatchingStoredStart ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-start-record-recovery",
            "late-start");
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var timeProvider = new ManualTimeProvider();
        var dispatchRequest = CreateLifecycleDispatchRequest(
            LifecycleExecutionKind.Compile,
            timeProvider,
            TimeSpan.FromSeconds(5));
        var waitTask = LifecycleExecutionStartRecordRecovery
            .WaitUntilAvailableAsync(
                unityProject,
                dispatchRequest,
                ExecutionDeadline.Start(
                    TimeSpan.FromSeconds(5),
                    timeProvider),
                CancellationToken.None);
        await Task.Yield();
        Assert.False(waitTask.IsCompleted);

        var persistedStart =
            await LifecycleExecutionIpcTestResponseFactory.PersistStartAsync(
                unityProject,
                dispatchRequest);
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            waitTask,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(10));
        var recoveredStart = await waitTask;

        Assert.NotNull(recoveredStart);
        Assert.Equal(
            persistedStart.LifecycleExecutionRef,
            recoveredStart!.LifecycleExecutionRef);
        Assert.Equal(persistedStart.Project, recoveredStart.Project);
        Assert.Equal(persistedStart.Host, recoveredStart.Host);
        Assert.Equal(
            persistedStart.StartedGeneration,
            recoveredStart.StartedGeneration);
    }
}
