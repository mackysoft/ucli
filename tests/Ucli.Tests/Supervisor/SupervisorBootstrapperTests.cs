using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.Supervisor;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorBootstrapperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureReady_WhenLaunchExceedsRemainingTimeout_ReturnsTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "launch-timeout");
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonCommandServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor ping should not be called before launch succeeds."),
        };
        var launcher = new StubSupervisorProcessLauncher
        {
            LaunchHandler = static async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return null;
            },
        };
        var bootstrapper = new SupervisorBootstrapper(
            new SupervisorManifestStore(),
            new SupervisorClient(transportClient),
            launcher,
            new SupervisorBootstrapLockProvider(),
            new SupervisorEndpointResolver());

        var result = await bootstrapper.EnsureReady(
            scope.FullPath,
            TimeSpan.FromMilliseconds(150),
            CancellationToken.None);

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
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonCommandServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called while manifest read is pending."),
        };
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: static async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                return null;
            },
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
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

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
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonCommandServiceTestContext.StubIpcTransportClient
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
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonCommandServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (endpoint, request, _, _) => ValueTask.FromResult(
                MackySoft.Ucli.Tests.Daemon.DaemonCommandServiceTestContext.CreateSuccessResponse(
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
        var launchCount = 0;
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: static (path, cancellationToken) => ValueTask.FromResult<string?>(null),
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonCommandServiceTestContext.StubIpcTransportClient
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
            new SupervisorEndpointResolver());

        var result = await bootstrapper.EnsureReady(
            scope.FullPath,
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
        Assert.Contains("did not publish a reachable manifest", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, launchCount);
    }

    private sealed class StubSupervisorProcessLauncher : ISupervisorProcessLauncher
    {
        public Func<CancellationToken, ValueTask<ExecutionError?>>? LaunchHandler { get; set; }

        public bool ObservedCancellation { get; private set; }

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
