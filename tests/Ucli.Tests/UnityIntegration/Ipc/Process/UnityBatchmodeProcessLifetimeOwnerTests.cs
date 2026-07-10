using MackySoft.Tests;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityBatchmodeProcessLifetimeOwnerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Transfer_WhenProcessIsRunning_KeepsHandleUntilProcessExits ()
    {
        var releaseProcess = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var handleDisposed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var processHandle = new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: cancellationToken => releaseProcess.Task.WaitAsync(cancellationToken))
        {
            OnDispose = handleDisposed.SetResult,
        };
        var owner = new UnityBatchmodeProcessLifetimeOwner();

        owner.Transfer(processHandle);

        Assert.Equal(0, processHandle.DisposeCount);
        releaseProcess.SetResult();
        await TestAwaiter.WaitAsync(
            handleDisposed.Task,
            "Owned batchmode process handle disposal",
            TimeSpan.FromSeconds(5));
        Assert.Equal(1, processHandle.DisposeCount);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Transfer_WhenMultipleProcessesRun_ReleasesEachHandleOnlyAfterItsOwnExit ()
    {
        var firstExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondExit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstHandle = new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: cancellationToken => firstExit.Task.WaitAsync(cancellationToken))
        {
            OnDispose = firstDisposed.SetResult,
        };
        var secondHandle = new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: cancellationToken => secondExit.Task.WaitAsync(cancellationToken))
        {
            OnDispose = secondDisposed.SetResult,
        };
        var owner = new UnityBatchmodeProcessLifetimeOwner();

        owner.Transfer(firstHandle);
        owner.Transfer(secondHandle);
        firstExit.SetResult();
        await TestAwaiter.WaitAsync(
            firstDisposed.Task,
            "First owned batchmode process handle disposal",
            TimeSpan.FromSeconds(5));

        Assert.Equal(1, firstHandle.DisposeCount);
        Assert.Equal(0, secondHandle.DisposeCount);

        secondExit.SetResult();
        await TestAwaiter.WaitAsync(
            secondDisposed.Task,
            "Second owned batchmode process handle disposal",
            TimeSpan.FromSeconds(5));
        Assert.Equal(1, secondHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Transfer_WhenExitObservationFaults_ReclaimsHandleAndReleasesOwnershipEntry ()
    {
        var firstDispose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processHandle = new StubUnityBatchmodeProcessHandle(
            waitForExitBehavior: static _ => Task.FromException(new IOException("exit observation failed")))
        {
            TerminateHandler = static (_, _) => Task.FromResult(ProcessTerminationResult.ForceKillFailed),
            OnDispose = firstDispose.SetResult,
        };
        var owner = new UnityBatchmodeProcessLifetimeOwner();

        owner.Transfer(processHandle);
        await TestAwaiter.WaitAsync(
            firstDispose.Task,
            "Faulted batchmode process ownership cleanup",
            TimeSpan.FromSeconds(5));

        UnityBatchmodeProcessHandleAssert.TerminatedOnceWithMode(
            processHandle,
            ProcessTerminationMode.ForceKill);
        Assert.Equal(1, processHandle.DisposeCount);

        var secondDispose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        processHandle.OnDispose = secondDispose.SetResult;
        owner.Transfer(processHandle);
        await TestAwaiter.WaitAsync(
            secondDispose.Task,
            "Retransferred batchmode process ownership cleanup",
            TimeSpan.FromSeconds(5));
        Assert.Equal(2, processHandle.DisposeCount);
    }
}
