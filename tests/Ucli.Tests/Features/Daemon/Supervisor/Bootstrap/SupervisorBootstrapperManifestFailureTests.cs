using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorBootstrapperManifestFailureTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenManifestReadFailsWithUnauthorizedAccess_ReturnsInternalError ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "manifest-read-unauthorized");
        var transportClient = new StubIpcTransportClient
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
            new RecordingSupervisorProcessLauncher(),
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
    [Trait("Size", "Medium")]
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
        var manifest = SupervisorBootstrapperTestSupport.CreateManifest(endpointAddress: maliciousPath);
        var manifestStore = new SupervisorManifestStore(
            readAllTextOrNull: (_, _) => ValueTask.FromResult<string?>(
                manifestDeleted ? null : JsonSerializer.Serialize(manifest)),
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: _ => manifestDeleted = true);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new SocketException((int)SocketError.ConnectionRefused),
        };
        var launcher = new RecordingSupervisorProcessLauncher
        {
            LaunchHandler = static (_, _) => ValueTask.FromResult<ExecutionError?>(
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
}
