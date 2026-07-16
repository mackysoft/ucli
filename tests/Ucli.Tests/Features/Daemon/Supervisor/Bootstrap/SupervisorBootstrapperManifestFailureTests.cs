using System.Net.Sockets;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorBootstrapperManifestFailureTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenValidUnreachableRuntimeOwnershipIsHeld_PreservesRuntimeWithoutRelaunch ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "owned-valid-runtime");
        var endpointResolver = new SupervisorEndpointResolver();
        var endpoint = endpointResolver.ResolveRuntimeEndpoint(
            scope.FullPath,
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1));
        var manifestStore = SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System);
        var manifest = SupervisorBootstrapperTestSupport.CreateManifest(endpoint: endpoint);
        await manifestStore.WriteAsync(scope.FullPath, manifest, CancellationToken.None);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(endpoint.Address)!);
            await File.WriteAllTextAsync(endpoint.Address, "owned endpoint", CancellationToken.None);
        }

        using var runtimeOwnership = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new SocketException((int)SocketError.ConnectionRefused),
        };
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchError = ExecutionError.InternalError("Owned valid runtime must not be relaunched."),
        };
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient, TimeProvider.System),
            processManager,
            new SupervisorBootstrapLockProvider(TimeProvider.System),
            endpointResolver,
            TimeProvider.System);

        var result = await bootstrapper.EnsureReadyAsync(
            scope.FullPath,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error?.Kind);
        Assert.Equal(manifest, await manifestStore.ReadOrNullAsync(scope.FullPath, CancellationToken.None));
        Assert.Empty(processManager.Invocations);
        Assert.NotEmpty(transportClient.Invocations);
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            Assert.True(File.Exists(endpoint.Address));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenMalformedRuntimeOwnershipIsHeld_PreservesArtifactWithoutRelaunch ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "owned-malformed-runtime");
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(scope.FullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(manifestPath, "{ malformed json", CancellationToken.None);
        using var runtimeOwnership = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(scope.FullPath),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new InvalidOperationException(
                "Malformed runtime ownership must block endpoint probing."),
        };
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchError = ExecutionError.InternalError("Owned malformed runtime must not be relaunched."),
        };
        var bootstrapper = new SupervisorBootstrapper(
            SupervisorManifestStoreTestSupport.CreateFileBacked(TimeProvider.System),
            new SupervisorClient(transportClient, TimeProvider.System),
            processManager,
            new SupervisorBootstrapLockProvider(TimeProvider.System),
            new SupervisorEndpointResolver(),
            TimeProvider.System);

        var result = await bootstrapper.EnsureReadyAsync(
            scope.FullPath,
            TimeSpan.FromMilliseconds(200),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error?.Kind);
        Assert.True(File.Exists(manifestPath));
        Assert.Empty(processManager.Invocations);
        Assert.Empty(transportClient.Invocations);
    }

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
            readAllBytesOrNull: static (_, _) => throw new UnauthorizedAccessException("manifest denied"),
            writeAllBytesAtomically: static (_, _, _) => ValueTask.CompletedTask,
            deleteIfExists: static _ => { });
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient, timeProvider),
            new RecordingSupervisorProcessManager(),
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
        var cleanupTarget = endpointResolver.ResolveUnixSocketCleanupTargetOrNull(scope.FullPath);
        if (cleanupTarget is not null)
        {
            var resolvedEndpointDirectoryPath = Path.GetDirectoryName(cleanupTarget.SocketPath);
            if (!string.IsNullOrWhiteSpace(resolvedEndpointDirectoryPath))
            {
                Directory.CreateDirectory(resolvedEndpointDirectoryPath);
            }

            File.WriteAllText(cleanupTarget.SocketPath, "stale supervisor socket placeholder");
        }

        var maliciousPath = cleanupTarget is not null
            ? Path.Combine(Path.GetDirectoryName(cleanupTarget.SocketPath)!, "x.sock")
            : scope.GetPath("do-not-delete.txt");
        File.WriteAllText(maliciousPath, "must remain");
        var manifestEndpoint = cleanupTarget is not null
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
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchHandler = static (_, _) => ValueTask.FromResult<ExecutionError?>(
                ExecutionError.InternalError("stop after cleanup")),
        };
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient, TimeProvider.System),
            processManager,
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
        if (cleanupTarget is not null)
        {
            Assert.False(File.Exists(cleanupTarget.SocketPath));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureReady_WhenManifestRotatesDuringProbe_PreservesAndUsesSuccessorGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-bootstrapper", "manifest-generation-rotation");
        var endpointResolver = new SupervisorEndpointResolver();
        var endpoint = endpointResolver.ResolveRuntimeEndpoint(
            scope.FullPath,
            IpcSessionTokenTestFactory.CreateFromDiscriminator(1));
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
                Assert.True(IpcSessionToken.TryParse(request.SessionToken, out var requestSessionToken));
                if (firstManifest.SessionToken == requestSessionToken)
                {
                    await manifestStore.WriteAsync(scope.FullPath, successorManifest, cancellationToken);
                    return CreateSessionTokenInvalidResponse(request.RequestId);
                }

                Assert.Equal(successorManifest.SessionToken, requestSessionToken);
                return CreatePingResponse(request.RequestId, successorManifest);
            },
        };
        var processManager = new RecordingSupervisorProcessManager
        {
            LaunchError = ExecutionError.InternalError("A successor manifest must be observed before relaunch."),
        };
        var bootstrapper = new SupervisorBootstrapper(
            manifestStore,
            new SupervisorClient(transportClient, TimeProvider.System),
            processManager,
            new SupervisorBootstrapLockProvider(TimeProvider.System),
            endpointResolver,
            TimeProvider.System);

        var result = await bootstrapper.EnsureReadyAsync(
            scope.FullPath,
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(successorManifest, result.Manifest);
        Assert.Empty(processManager.Invocations);
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
            status: IpcResponseStatus.Error,
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
            status: IpcResponseStatus.Ok,
            payload: IpcPayloadCodec.SerializeToElement(new SupervisorIpcContracts.PingResponse(
                manifest.ProcessId,
                manifest.IssuedAtUtc)),
            errors: Array.Empty<IpcError>());
    }
}
