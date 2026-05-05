using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.UnityIntegration.Ipc;

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

        var resultTask = bootstrapper.EnsureReady(
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

        var resultTask = bootstrapper.EnsureReady(
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

        var result = await bootstrapper.EnsureReady(
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
    public async Task EnsureReady_WhenInitialLaunchDoesNotPublishManifest_RelaunchesAndSucceeds ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "relaunch-success");
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
            new SupervisorEndpointResolver());

        var result = await bootstrapper.EnsureReady(
            scope.FullPath,
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Manifest);
        Assert.Equal(2, launchCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureReady_WhenLaunchNeverPublishesManifest_ReturnsInternalErrorBeforeTimeout ()
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

        var resultTask = bootstrapper.EnsureReady(
                scope.FullPath,
                TimeSpan.FromSeconds(2),
                CancellationToken.None)
            .AsTask();
        for (var i = 0; i < 4; i++)
        {
            await WaitForActiveTimerAsync(timeProvider, resultTask, CancellationToken.None);
            if (resultTask.IsCompleted)
            {
                break;
            }

            timeProvider.Advance(SupervisorConstants.BootstrapPollDelay);
        }

        var result = await TestAwaiter.WaitAsync(resultTask, "Supervisor manifest publication failure result", SignalWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
        Assert.Contains("did not publish a reachable manifest", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, launchCount);
    }

    private static async Task WaitForActiveTimerAsync (
        ManualTimeProvider timeProvider,
        Task observedTask,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (observedTask.IsCompleted || timeProvider.ActiveTimerCount > 0)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        Assert.True(
            observedTask.IsCompleted || timeProvider.ActiveTimerCount > 0,
            "Supervisor bootstrap did not register the expected poll delay timer.");
    }

    private sealed class StubSupervisorProcessLauncher : ISupervisorProcessLauncher
    {
        public Func<CancellationToken, ValueTask<ExecutionError?>>? LaunchHandler { get; set; }

        public bool ObservedCancellation { get; private set; }

        public TaskCompletionSource? LaunchStarted { get; set; }

        public async ValueTask<ExecutionError?> Launch (
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
