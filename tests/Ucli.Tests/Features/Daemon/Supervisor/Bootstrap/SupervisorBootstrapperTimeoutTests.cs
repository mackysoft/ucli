using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorBootstrapperTimeoutTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenLaunchExceedsRemainingTimeout_ReturnsTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "launch-timeout");
        var timeProvider = new ManualTimeProvider();
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor ping should not be called before launch succeeds."),
        };
        var launchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchHandler = static async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return SupervisorProcessLaunchResult.Failure(
                    ExecutionError.InternalError("Unreachable launch result."));
            },
            LaunchStarted = launchStarted,
        };
        var bootstrapper = new SupervisorBootstrapper(
            SupervisorManifestStoreTestSupport.CreateFileBacked(timeProvider),
            new SupervisorClient(transportClient, timeProvider),
            processManager,
            new SupervisorBootstrapLockProvider(timeProvider),
            new SupervisorEndpointResolver(),
            timeProvider);

        var resultTask = bootstrapper.EnsureReadyAsync(
                scope.FullPath,
                TimeSpan.FromMilliseconds(150),
                CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(launchStarted.Task, "Supervisor launch start", SupervisorBootstrapperTestSupport.SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor launch timeout result", SupervisorBootstrapperTestSupport.SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error.Kind);
        Assert.True(processManager.ObservedCancellation);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenManifestReadExceedsRemainingTimeout_ReturnsTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "manifest-read-timeout");
        var timeProvider = new ManualTimeProvider();
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called while manifest read is pending."),
        };
        var manifestReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manifestStore = new SupervisorManifestStore(
            timeProvider,
            readAllBytesOrNull: async (_, cancellationToken) =>
            {
                manifestReadStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return null;
            },
            writeAllBytesAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient, timeProvider),
            new RecordingSupervisorProcessManager(),
            new SupervisorBootstrapLockProvider(timeProvider),
            new SupervisorEndpointResolver(),
            timeProvider);

        var resultTask = bootstrapper.EnsureReadyAsync(
                scope.FullPath,
                TimeSpan.FromMilliseconds(500),
                CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(manifestReadStarted.Task, "Supervisor manifest read start", SupervisorBootstrapperTestSupport.SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(500));

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor manifest read timeout result", SupervisorBootstrapperTestSupport.SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error.Kind);
        Assert.Contains(
            "Timed out while reading supervisor manifest.",
            result.Error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenLaunchPollDelayWouldExceedDeadline_ReturnsTimeoutAtDeadline ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "launch-poll-deadline");
        var timeProvider = new ManualTimeProvider();
        var releaseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAllowed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launchLease = new RecordingSupervisorProcessLaunchLease
        {
            RollbackHandler = async () =>
            {
                releaseStarted.TrySetResult();
                await releaseAllowed.Task.ConfigureAwait(false);
                return null;
            },
        };
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called without a manifest."),
        };
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchHandler = (_, _) => ValueTask.FromResult(
                SupervisorProcessLaunchResult.Success(launchLease)),
        };
        var bootstrapper = new SupervisorBootstrapper(
            SupervisorManifestStoreTestSupport.CreateFileBacked(timeProvider),
            new SupervisorClient(transportClient, timeProvider),
            processManager,
            new SupervisorBootstrapLockProvider(timeProvider),
            new SupervisorEndpointResolver(),
            timeProvider);

        var resultTask = bootstrapper.EnsureReadyAsync(
                scope.FullPath,
                TimeSpan.FromMilliseconds(50),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.WaitForTimerDueWithinOrCompletionAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromMilliseconds(50));
        if (!resultTask.IsCompleted)
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        }

        await TestAwaiter.WaitAsync(
            releaseStarted.Task,
            "Supervisor launch registration rollback start",
            SupervisorBootstrapperTestSupport.SignalWaitTimeout);
        Assert.False(resultTask.IsCompleted);
        releaseAllowed.TrySetResult();

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor poll deadline result", SupervisorBootstrapperTestSupport.SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error.Kind);
        Assert.Equal(1, launchLease.RollbackCount);
        Assert.Equal(0, launchLease.CommitCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenCanceledAfterLaunch_RollsBackRegistration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "post-launch-cancellation");
        var timeProvider = new ManualTimeProvider();
        var launchLease = new RecordingSupervisorProcessLaunchLease();
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchHandler = (_, _) => ValueTask.FromResult(
                SupervisorProcessLaunchResult.Success(launchLease)),
        };
        var bootstrapper = new SupervisorBootstrapper(
            SupervisorManifestStoreTestSupport.CreateFileBacked(timeProvider),
            new SupervisorClient(new StubIpcTransportClient(), timeProvider),
            processManager,
            new SupervisorBootstrapLockProvider(timeProvider),
            new SupervisorEndpointResolver(),
            timeProvider);
        using var cancellation = new CancellationTokenSource();
        var resultTask = bootstrapper.EnsureReadyAsync(
                scope.FullPath,
                TimeSpan.FromSeconds(5),
                cancellation.Token)
            .AsTask();
        await timeProvider.WaitForTimerDueWithinAsync(SupervisorConstants.BootstrapPollDelay)
            .WaitAsync(SupervisorBootstrapperTestSupport.SignalWaitTimeout);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resultTask);
        Assert.Equal(1, launchLease.RollbackCount);
        Assert.Equal(0, launchLease.CommitCount);
    }
}
