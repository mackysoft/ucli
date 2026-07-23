using MackySoft.FileSystem;
using System.Text;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorBootstrapperLaunchRecoveryTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenManifestAppearsDuringLaunchGrace_DoesNotRelaunch ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "delayed-manifest");
        var timeProvider = new ManualTimeProvider();
        var manifestPublicationTime = DateTimeOffset.UnixEpoch + SupervisorConstants.BootstrapPollDelay;
        var launchCount = 0;
        var manifest = SupervisorBootstrapperTestSupport.CreateManifest();
        var manifestStore = new SupervisorManifestStore(
            timeProvider,
            readAllBytesOrNull: (path, cancellationToken) => ValueTask.FromResult<ReadOnlyMemory<byte>?>(
                timeProvider.GetUtcNow() >= manifestPublicationTime
                    ? Encoding.UTF8.GetBytes(SupervisorManifestStoreTestSupport.Serialize(manifest))
                    : null),
            writeAllBytesAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var transportClient = CreatePingTransport(manifest);
        var launchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launchLease = new RecordingSupervisorProcessLaunchLease();
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchHandler = (_, _) =>
            {
                launchCount++;
                return ValueTask.FromResult(SupervisorProcessLaunchResult.Success(launchLease));
            },
            LaunchStarted = launchStarted,
        };
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient, TimeProvider.System),
            processManager,
            new SupervisorBootstrapLockProvider(timeProvider),
            new SupervisorEndpointResolver(),
            timeProvider);

        var resultTask = bootstrapper.EnsureReadyAsync(
                AbsolutePath.Parse(scope.FullPath),
                TimeSpan.FromSeconds(30),
                CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(launchStarted.Task, "Supervisor delayed manifest launch start", SupervisorBootstrapperTestSupport.SignalWaitTimeout);
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            SupervisorConstants.BootstrapPollDelay + SupervisorConstants.BootstrapPollDelay,
            SupervisorConstants.BootstrapPollDelay);

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor delayed manifest result", SupervisorBootstrapperTestSupport.SignalWaitTimeout);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Manifest);
        Assert.Equal(1, launchCount);
        Assert.Equal(1, launchLease.CommitCount);
        Assert.Equal(0, launchLease.RollbackCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenInitialLaunchDoesNotPublishManifest_RelaunchesAndSucceeds ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "relaunch-success");
        var timeProvider = new ManualTimeProvider();
        var launchCount = 0;
        var events = new List<string>();
        var firstLaunchLease = new RecordingSupervisorProcessLaunchLease
        {
            RollbackHandler = () =>
            {
                events.Add("rollback:first");
                return ValueTask.FromResult<ExecutionError?>(null);
            },
            CommitHandler = () =>
            {
                events.Add("commit:first");
                return ValueTask.CompletedTask;
            },
        };
        var secondLaunchLease = new RecordingSupervisorProcessLaunchLease
        {
            RollbackHandler = () =>
            {
                events.Add("rollback:second");
                return ValueTask.FromResult<ExecutionError?>(null);
            },
            CommitHandler = () =>
            {
                events.Add("commit:second");
                return ValueTask.CompletedTask;
            },
        };
        var manifest = SupervisorBootstrapperTestSupport.CreateManifest();
        var manifestStore = new SupervisorManifestStore(
            timeProvider,
            readAllBytesOrNull: (path, cancellationToken) => ValueTask.FromResult<ReadOnlyMemory<byte>?>(
                launchCount >= 2 ? Encoding.UTF8.GetBytes(SupervisorManifestStoreTestSupport.Serialize(manifest)) : null),
            writeAllBytesAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var transportClient = CreatePingTransport(manifest);
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchHandler = (_, _) =>
            {
                launchCount++;
                events.Add($"launch:{launchCount}");
                var lease = launchCount == 1
                    ? firstLaunchLease
                    : secondLaunchLease;
                return ValueTask.FromResult(SupervisorProcessLaunchResult.Success(lease));
            },
        };
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient, TimeProvider.System),
            processManager,
            new SupervisorBootstrapLockProvider(timeProvider),
            new SupervisorEndpointResolver(),
            timeProvider);

        var resultTask = bootstrapper.EnsureReadyAsync(
                AbsolutePath.Parse(scope.FullPath),
                TimeSpan.FromSeconds(60),
                CancellationToken.None)
            .AsTask();
        for (var i = 0; i < 10 && !resultTask.IsCompleted; i++)
        {
            await ManualTimeTaskDriver.WaitForTimerDueWithinOrCompletionAsync(
                timeProvider,
                resultTask,
                SupervisorConstants.ManifestPublicationTimeout);
            if (!resultTask.IsCompleted)
            {
                timeProvider.Advance(
                    launchCount < 2
                        ? SupervisorConstants.ManifestPublicationTimeout
                        : SupervisorConstants.BootstrapPollDelay);
            }
        }

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor relaunch success result", SupervisorBootstrapperTestSupport.SignalWaitTimeout);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Manifest);
        Assert.Equal(2, launchCount);
        Assert.Equal(
            ["launch:1", "rollback:first", "launch:2", "commit:second"],
            events);
        Assert.Equal(0, firstLaunchLease.CommitCount);
        Assert.Equal(0, secondLaunchLease.RollbackCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenLaunchNeverPublishesManifest_ReturnsInternalErrorAfterLaunchGrace ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "launch-no-manifest");
        var timeProvider = new ManualTimeProvider();
        var launchCount = 0;
        var launchLeases = new[]
        {
            new RecordingSupervisorProcessLaunchLease(),
            new RecordingSupervisorProcessLaunchLease(),
        };
        var manifestStore = new SupervisorManifestStore(
            timeProvider,
            readAllBytesOrNull: static (path, cancellationToken) => ValueTask.FromResult<ReadOnlyMemory<byte>?>(null),
            writeAllBytesAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called without a manifest."),
        };
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchHandler = (_, _) =>
            {
                launchCount++;
                return ValueTask.FromResult(
                    SupervisorProcessLaunchResult.Success(launchLeases[launchCount - 1]));
            },
        };
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient, TimeProvider.System),
            processManager,
            new SupervisorBootstrapLockProvider(timeProvider),
            new SupervisorEndpointResolver(),
            timeProvider);

        var resultTask = bootstrapper.EnsureReadyAsync(
                AbsolutePath.Parse(scope.FullPath),
                TimeSpan.FromSeconds(60),
                CancellationToken.None)
            .AsTask();
        for (var i = 0; i < 10 && !resultTask.IsCompleted; i++)
        {
            await ManualTimeTaskDriver.WaitForTimerDueWithinOrCompletionAsync(
                timeProvider,
                resultTask,
                SupervisorConstants.ManifestPublicationTimeout);
            if (!resultTask.IsCompleted)
            {
                timeProvider.Advance(SupervisorConstants.ManifestPublicationTimeout);
            }
        }

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor manifest publication failure result", SupervisorBootstrapperTestSupport.SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
        Assert.Contains("did not publish a reachable manifest", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, launchCount);
        Assert.All(launchLeases, static lease => Assert.Equal(1, lease.RollbackCount));
        Assert.All(launchLeases, static lease => Assert.Equal(0, lease.CommitCount));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenPreviousLaunchRollbackFails_DoesNotCreateAnotherGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "rollback-failure");
        var timeProvider = new ManualTimeProvider();
        var rollbackAttemptCount = 0;
        var launchLease = new RecordingSupervisorProcessLaunchLease
        {
            RollbackHandler = () =>
            {
                rollbackAttemptCount++;
                return ValueTask.FromResult<ExecutionError?>(
                    rollbackAttemptCount == 1
                        ? ExecutionError.InternalError("Injected rollback failure.")
                        : null);
            },
        };
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

        var resultTask = bootstrapper.EnsureReadyAsync(
                AbsolutePath.Parse(scope.FullPath),
                TimeSpan.FromSeconds(30),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.WaitForTimerDueWithinOrCompletionAsync(
            timeProvider,
            resultTask,
            SupervisorConstants.ManifestPublicationTimeout);
        if (!resultTask.IsCompleted)
        {
            timeProvider.Advance(SupervisorConstants.ManifestPublicationTimeout);
        }

        var result = await TestAwaiter.WaitAsync(
            resultTask,
            "Supervisor rollback failure result",
            SupervisorBootstrapperTestSupport.SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.Equal("Injected rollback failure.", result.Error!.Message);
        Assert.Single(processManager.Invocations);
        Assert.Equal(2, launchLease.RollbackCount);
        Assert.Equal(0, launchLease.CommitCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenFailedLaunchRetainsGenerationLease_RollsItBackBeforeReturning ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "failed-launch-lease");
        var timeProvider = new ManualTimeProvider();
        var launchLease = new RecordingSupervisorProcessLaunchLease();
        var launchError = ExecutionError.InternalError("Injected launch failure with unresolved ownership.");
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchHandler = (_, _) => ValueTask.FromResult(
                SupervisorProcessLaunchResult.FailureWithLease(launchError, launchLease)),
        };
        var bootstrapper = new SupervisorBootstrapper(
            SupervisorManifestStoreTestSupport.CreateFileBacked(timeProvider),
            new SupervisorClient(new StubIpcTransportClient(), timeProvider),
            processManager,
            new SupervisorBootstrapLockProvider(timeProvider),
            new SupervisorEndpointResolver(),
            timeProvider);

        var result = await bootstrapper.EnsureReadyAsync(
            AbsolutePath.Parse(scope.FullPath),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(launchError, result.Error);
        Assert.Equal(1, launchLease.RollbackCount);
        Assert.Equal(0, launchLease.CommitCount);
    }

    private static StubIpcTransportClient CreatePingTransport (SupervisorInstanceManifest manifest)
    {
        return new StubIpcTransportClient
        {
            SendHandler = (endpoint, request, _, _) => ValueTask.FromResult(
                IpcResponseTestFactory.CreateSuccess(
                    request,
                    new SupervisorIpcContracts.PingResponse(manifest.ProcessId, manifest.IssuedAtUtc))),
        };
    }
}
