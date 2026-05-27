using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorBootstrapperTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureReady_WhenLaunchExceedsRemainingTimeout_ReturnsTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "launch-timeout");
        var timeProvider = new ManualTimeProvider();
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor ping should not be called before launch succeeds."),
        };
        var launchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launcher = new StubSupervisorProcessLauncher
        {
            LaunchHandler = static async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return null;
            },
            LaunchStarted = launchStarted,
        };
        var bootstrapper = new SupervisorBootstrapper(
            new SupervisorManifestStore(),
            new SupervisorClient(transportClient),
            launcher,
            new SupervisorBootstrapLockProvider(),
            new SupervisorEndpointResolver(),
            timeProvider);

        var resultTask = bootstrapper.EnsureReadyAsync(
                scope.FullPath,
                TimeSpan.FromMilliseconds(150),
                CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(launchStarted.Task, "Supervisor launch start", SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor launch timeout result", SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error.Kind);
        Assert.True(launcher.ObservedCancellation);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureReady_WhenManifestReadExceedsRemainingTimeout_ReturnsTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "manifest-read-timeout");
        var timeProvider = new ManualTimeProvider();
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called while manifest read is pending."),
        };
        var manifestReadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: async (_, cancellationToken) =>
            {
                manifestReadStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return null;
            },
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { },
            timeProvider: timeProvider);
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient),
            new StubSupervisorProcessLauncher(),
            new SupervisorBootstrapLockProvider(),
            new SupervisorEndpointResolver(),
            timeProvider);

        var resultTask = bootstrapper.EnsureReadyAsync(
                scope.FullPath,
                TimeSpan.FromMilliseconds(500),
                CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(manifestReadStarted.Task, "Supervisor manifest read start", SignalWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(500));

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor manifest read timeout result", SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error.Kind);
        Assert.Contains(
            "Timed out while reading supervisor manifest.",
            result.Error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureReady_WhenManifestReadFailsWithUnauthorizedAccess_ReturnsInternalError ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "manifest-read-unauthorized");
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called when manifest read fails."),
        };
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: static (_, _) => throw new UnauthorizedAccessException("manifest denied"),
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient),
            new StubSupervisorProcessLauncher(),
            new SupervisorBootstrapLockProvider(),
            new SupervisorEndpointResolver());

        var result = await bootstrapper.EnsureReadyAsync(
            scope.FullPath,
            TimeSpan.FromMilliseconds(150),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
        Assert.Contains("Failed to read supervisor manifest", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureReady_WhenManifestAppearsDuringLaunchGrace_DoesNotRelaunch ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "delayed-manifest");
        var timeProvider = new ManualTimeProvider();
        var manifestPublicationTime = DateTimeOffset.UnixEpoch + SupervisorConstants.BootstrapPollDelay;
        var launchCount = 0;
        var manifest = new SupervisorInstanceManifest(
            ProcessId: 2468,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli-supervisor-test.sock",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: (path, cancellationToken) => ValueTask.FromResult<string?>(
                timeProvider.GetUtcNow() >= manifestPublicationTime
                    ? JsonSerializer.Serialize(manifest)
                    : null),
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { },
            timeProvider: timeProvider);
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (endpoint, request, _, _) => ValueTask.FromResult(
                MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.CreateSuccessResponse(
                    request,
                    new SupervisorIpcContracts.PingResponse(manifest.ProcessId, manifest.IssuedAtUtc))),
        };
        var launchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launcher = new StubSupervisorProcessLauncher
        {
            LaunchHandler = _ =>
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
        await TestAwaiter.WaitAsync(launchStarted.Task, "Supervisor delayed manifest launch start", SignalWaitTimeout);
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            SupervisorConstants.BootstrapPollDelay + SupervisorConstants.BootstrapPollDelay,
            SupervisorConstants.BootstrapPollDelay);

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor delayed manifest result", SignalWaitTimeout);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Manifest);
        Assert.Equal(1, launchCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureReady_WhenInitialLaunchDoesNotPublishManifest_RelaunchesAndSucceeds ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "relaunch-success");
        var timeProvider = new ManualTimeProvider();
        var launchCount = 0;
        var manifest = new SupervisorInstanceManifest(
            ProcessId: 2468,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli-supervisor-test.sock",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: (path, cancellationToken) => ValueTask.FromResult<string?>(
                launchCount >= 2 ? JsonSerializer.Serialize(manifest) : null),
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (endpoint, request, _, _) => ValueTask.FromResult(
                MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.CreateSuccessResponse(
                    request,
                    new SupervisorIpcContracts.PingResponse(manifest.ProcessId, manifest.IssuedAtUtc))),
        };
        var launcher = new StubSupervisorProcessLauncher
        {
            LaunchHandler = _ =>
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

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor relaunch success result", SignalWaitTimeout);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Manifest);
        Assert.Equal(2, launchCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureReady_WhenLaunchNeverPublishesManifest_ReturnsInternalErrorAfterLaunchGrace ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "launch-no-manifest");
        var timeProvider = new ManualTimeProvider();
        var launchCount = 0;
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: static (path, cancellationToken) => ValueTask.FromResult<string?>(null),
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called without a manifest."),
        };
        var launcher = new StubSupervisorProcessLauncher
        {
            LaunchHandler = _ =>
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

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor manifest publication failure result", SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
        Assert.Contains("did not publish a reachable manifest", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, launchCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureReady_WhenLaunchPollDelayWouldExceedDeadline_ReturnsTimeoutAtDeadline ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "launch-poll-deadline");
        var timeProvider = new ManualTimeProvider();
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called without a manifest."),
        };
        var launcher = new StubSupervisorProcessLauncher
        {
            LaunchHandler = static _ => ValueTask.FromResult<ExecutionError?>(null),
        };
        var bootstrapper = new SupervisorBootstrapper(
            new SupervisorManifestStore(),
            new SupervisorClient(transportClient),
            launcher,
            new SupervisorBootstrapLockProvider(),
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

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor poll deadline result", SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureReady_WhenManifestIsUnreachable_DeletesOnlyResolvedSupervisorEndpoint ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "stale-manifest-cleanup");
        var endpointResolver = new SupervisorEndpointResolver();
        var resolvedEndpoint = endpointResolver.Resolve(scope.FullPath);
        if (resolvedEndpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            var resolvedEndpointDirectoryPath = Path.GetDirectoryName(resolvedEndpoint.Address);
            if (!string.IsNullOrWhiteSpace(resolvedEndpointDirectoryPath))
            {
                Directory.CreateDirectory(resolvedEndpointDirectoryPath);
            }

            File.WriteAllText(resolvedEndpoint.Address, "stale supervisor socket placeholder");
        }

        var manifestDeleted = false;
        var maliciousPath = scope.GetPath("do-not-delete.txt");
        File.WriteAllText(maliciousPath, "must remain");
        var manifest = new SupervisorInstanceManifest(
            ProcessId: 2468,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: maliciousPath,
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: (_, _) => ValueTask.FromResult<string?>(
                manifestDeleted ? null : JsonSerializer.Serialize(manifest)),
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: _ => manifestDeleted = true);
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new SocketException((int)SocketError.ConnectionRefused),
        };
        var launcher = new StubSupervisorProcessLauncher
        {
            LaunchHandler = static _ => ValueTask.FromResult<ExecutionError?>(
                ExecutionError.InternalError("stop after cleanup")),
        };
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient),
            launcher,
            new SupervisorBootstrapLockProvider(),
            endpointResolver);

        var result = await bootstrapper.EnsureReadyAsync(
            scope.FullPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(manifestDeleted);
        Assert.True(File.Exists(maliciousPath));
        if (resolvedEndpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.False(File.Exists(resolvedEndpoint.Address));
        }
    }

    private sealed class StubSupervisorProcessLauncher : ISupervisorProcessLauncher
    {
        public Func<CancellationToken, ValueTask<ExecutionError?>>? LaunchHandler { get; set; }

        public bool ObservedCancellation { get; private set; }

        public TaskCompletionSource? LaunchStarted { get; set; }

        public async ValueTask<ExecutionError?> LaunchAsync (
            string storageRoot,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (LaunchHandler == null)
                {
                    throw new InvalidOperationException("Launch handler is not configured.");
                }

                LaunchStarted?.TrySetResult();
                return await LaunchHandler(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ObservedCancellation = true;
                throw;
            }
        }
    }

}
