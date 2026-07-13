using System.Net.Sockets;
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
        var timeProvider = new ManualTimeProvider();
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException("Supervisor transport should not be called when manifest read fails."),
        };
        var manifestStore = new SupervisorManifestStore(
            timeProvider,
            readAllTextOrNull: static (_, _) => throw new UnauthorizedAccessException("manifest denied"),
            writeAllTextAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient, timeProvider),
            new RecordingSupervisorProcessLauncher(),
            new SupervisorBootstrapLockProvider(timeProvider),
            new SupervisorEndpointResolver(),
            timeProvider);

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
        var resolvedEndpoint = endpointResolver.ResolveCanonicalEndpoint(scope.FullPath);
        if (resolvedEndpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            var resolvedEndpointDirectoryPath = Path.GetDirectoryName(resolvedEndpoint.Address);
            if (!string.IsNullOrWhiteSpace(resolvedEndpointDirectoryPath))
            {
                Directory.CreateDirectory(resolvedEndpointDirectoryPath);
            }

            File.WriteAllText(resolvedEndpoint.Address, "stale supervisor socket placeholder");
        }

        var maliciousPath = resolvedEndpoint.TransportKind == IpcTransportKind.UnixDomainSocket
            ? Path.Combine(Path.GetDirectoryName(resolvedEndpoint.Address)!, "x.sock")
            : scope.GetPath("do-not-delete.txt");
        File.WriteAllText(maliciousPath, "must remain");
        var manifestEndpoint = resolvedEndpoint.TransportKind == IpcTransportKind.UnixDomainSocket
            ? new IpcEndpoint(IpcTransportKind.UnixDomainSocket, maliciousPath)
            : new IpcEndpoint(IpcTransportKind.NamedPipe, $"ucli-do-not-delete-{Guid.NewGuid():N}");
        var manifest = SupervisorBootstrapperTestSupport.CreateManifest(
            endpoint: manifestEndpoint);
        var manifestStore = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        await manifestStore.WriteAsync(scope.FullPath, manifest, CancellationToken.None);
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
            new SupervisorClient(transportClient, TimeProvider.System),
            launcher,
            new SupervisorBootstrapLockProvider(TimeProvider.System),
            endpointResolver,
            TimeProvider.System);

        var result = await bootstrapper.EnsureReadyAsync(
            scope.FullPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(await manifestStore.ReadOrNullAsync(scope.FullPath, CancellationToken.None));
        Assert.True(File.Exists(maliciousPath));
        if (resolvedEndpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.False(File.Exists(resolvedEndpoint.Address));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenManifestRotatesDuringProbe_PreservesAndUsesSuccessorGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "manifest-generation-rotation");
        var endpointResolver = new SupervisorEndpointResolver();
        var endpoint = endpointResolver.ResolveCanonicalEndpoint(scope.FullPath);
        var manifestStore = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var firstManifest = SupervisorBootstrapperTestSupport.CreateManifest(
            endpoint: endpoint);
        var successorManifest = SupervisorBootstrapperTestSupport.CreateManifest(
            sessionTokenDiscriminator: 2,
            processId: firstManifest.ProcessId,
            endpoint: firstManifest.Endpoint,
            issuedAtUtc: firstManifest.IssuedAtUtc.AddSeconds(1));
        await manifestStore.WriteAsync(scope.FullPath, firstManifest, CancellationToken.None);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            var endpointDirectory = Path.GetDirectoryName(endpoint.Address)!;
            Directory.CreateDirectory(endpointDirectory);
            await File.WriteAllTextAsync(endpoint.Address, "successor endpoint", CancellationToken.None);
        }

        var transportClient = new StubIpcTransportClient
        {
            SendHandler = async (_, request, _, cancellationToken) =>
            {
                if (firstManifest.SessionToken.Matches(request.SessionToken))
                {
                    await manifestStore.WriteAsync(scope.FullPath, successorManifest, cancellationToken);
                    return CreateSessionTokenInvalidResponse(request.RequestId);
                }

                Assert.Equal(successorManifest.SessionToken.GetEncodedValue(), request.SessionToken);
                return CreatePingResponse(request.RequestId, successorManifest);
            },
        };
        var launcher = new RecordingSupervisorProcessLauncher
        {
            LaunchError = ExecutionError.InternalError("A successor manifest must be observed before relaunch."),
        };
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient, TimeProvider.System),
            launcher,
            new SupervisorBootstrapLockProvider(TimeProvider.System),
            endpointResolver,
            TimeProvider.System);

        var result = await bootstrapper.EnsureReadyAsync(
            scope.FullPath,
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(successorManifest, result.Manifest);
        Assert.Empty(launcher.Invocations);
        Assert.Equal(
            successorManifest,
            await manifestStore.ReadOrNullAsync(scope.FullPath, CancellationToken.None));
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.True(File.Exists(endpoint.Address));
        }
    }

    private static IpcResponse CreateSessionTokenInvalidResponse (Guid requestId)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcProtocol.StatusError,
            payload: IpcPayloadCodec.SerializeToElement(new UcliEmptyArgs()),
            errors:
            [
                new IpcError(
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "Session token is invalid.",
                    OpId: null),
            ]);
    }

    private static IpcResponse CreatePingResponse (
        Guid requestId,
        SupervisorInstanceManifest manifest)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcProtocol.StatusOk,
            payload: IpcPayloadCodec.SerializeToElement(new SupervisorIpcContracts.PingResponse(
                manifest.ProcessId,
                manifest.IssuedAtUtc)),
            errors: Array.Empty<IpcError>());
    }
}
