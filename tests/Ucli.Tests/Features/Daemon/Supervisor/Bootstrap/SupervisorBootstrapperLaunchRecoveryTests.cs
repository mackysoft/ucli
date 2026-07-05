using System.Text.Json;
using MackySoft.Tests;
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
            readAllTextOrNull: (path, cancellationToken) => ValueTask.FromResult<string?>(
                timeProvider.GetUtcNow() >= manifestPublicationTime
                    ? JsonSerializer.Serialize(manifest)
                    : null),
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { },
            timeProvider: timeProvider);
        var transportClient = CreatePingTransport(manifest);
        var launchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launcher = new RecordingSupervisorProcessLauncher
        {
            LaunchHandler = (_, _) =>
            {
                launchCount++;
                return ValueTask.FromResult<ExecutionError?>(null);
            },
            LaunchStarted = launchStarted,
        };
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient),
            launcher,
            new SupervisorBootstrapLockProvider(),
            new SupervisorEndpointResolver(),
            timeProvider);

        var resultTask = bootstrapper.EnsureReadyAsync(
                scope.FullPath,
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
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenInitialLaunchDoesNotPublishManifest_RelaunchesAndSucceeds ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "relaunch-success");
        var timeProvider = new ManualTimeProvider();
        var launchCount = 0;
        var manifest = SupervisorBootstrapperTestSupport.CreateManifest();
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: (path, cancellationToken) => ValueTask.FromResult<string?>(
                launchCount >= 2 ? JsonSerializer.Serialize(manifest) : null),
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var transportClient = CreatePingTransport(manifest);
        var launcher = new RecordingSupervisorProcessLauncher
        {
            LaunchHandler = (_, _) =>
            {
                launchCount++;
                return ValueTask.FromResult<ExecutionError?>(null);
            },
        };
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient),
            launcher,
            new SupervisorBootstrapLockProvider(),
            new SupervisorEndpointResolver(),
            timeProvider);

        var resultTask = bootstrapper.EnsureReadyAsync(
                scope.FullPath,
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
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenLaunchNeverPublishesManifest_ReturnsInternalErrorAfterLaunchGrace ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "launch-no-manifest");
        var timeProvider = new ManualTimeProvider();
        var launchCount = 0;
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: static (path, cancellationToken) => ValueTask.FromResult<string?>(null),
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called without a manifest."),
        };
        var launcher = new RecordingSupervisorProcessLauncher
        {
            LaunchHandler = (_, _) =>
            {
                launchCount++;
                return ValueTask.FromResult<ExecutionError?>(null);
            },
        };
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient),
            launcher,
            new SupervisorBootstrapLockProvider(),
            new SupervisorEndpointResolver(),
            timeProvider);

        var resultTask = bootstrapper.EnsureReadyAsync(
                scope.FullPath,
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
