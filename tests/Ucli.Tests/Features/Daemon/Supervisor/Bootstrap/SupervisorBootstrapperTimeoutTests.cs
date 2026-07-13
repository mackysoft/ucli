using MackySoft.Tests;
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
        var launcher = new RecordingSupervisorProcessLauncher
        {
            LaunchHandler = static async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return null;
            },
            LaunchStarted = launchStarted,
        };
        var bootstrapper = new SupervisorBootstrapper(
            SupervisorManifestStoreTestSupport.CreateFileBacked(timeProvider),
            new SupervisorClient(transportClient, timeProvider),
            launcher,
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
        Assert.True(launcher.ObservedCancellation);
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
            new RecordingSupervisorProcessLauncher(),
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
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called without a manifest."),
        };
        var launcher = new RecordingSupervisorProcessLauncher
        {
            LaunchHandler = static (_, _) => ValueTask.FromResult<ExecutionError?>(null),
        };
        var bootstrapper = new SupervisorBootstrapper(
            SupervisorManifestStoreTestSupport.CreateFileBacked(timeProvider),
            new SupervisorClient(transportClient, timeProvider),
            launcher,
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

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor poll deadline result", SupervisorBootstrapperTestSupport.SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error.Kind);
    }
}
