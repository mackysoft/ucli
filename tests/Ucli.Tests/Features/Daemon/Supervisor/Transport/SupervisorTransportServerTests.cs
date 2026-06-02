using System.Runtime.Versioning;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorTransportServerTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WhenOneConnectionBlocks_StillAcceptsAnotherConnection ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-transport-server", "parallel-accept");
        var endpoint = CreateEndpoint(scope.FullPath);
        var server = new SupervisorTransportServer();
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowRequestEnteredTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSlowRequestTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            endpoint,
            async (stream, cancellationToken) =>
            {
                var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequest>(
                        stream,
                        IpcJsonSerializerOptions.Default,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                Assert.True(readResult.IsSuccess);

                var request = readResult.Value;
                if (request.Method == "slow")
                {
                    slowRequestEnteredTaskSource.TrySetResult();
                    await releaseSlowRequestTaskSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                var response = new IpcResponse(
                    ProtocolVersion: request.ProtocolVersion,
                    RequestId: request.RequestId,
                    Status: IpcProtocol.StatusOk,
                    Payload: IpcPayloadCodec.SerializeToElement(new TransportServerResponse(request.Method)),
                    Errors: Array.Empty<IpcError>());
                await IpcFrameCodec.WriteModelAsync(
                        stream,
                        response,
                        IpcJsonSerializerOptions.Default,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            cancellationTokenSource.Token);

        try
        {
            await TestAwaiter.WaitAsync(startedTaskSource.Task, "Supervisor transport start", SignalWaitTimeout);

            var client = new IpcTransportClient();
            var slowRequestTask = client.SendAsync(
                    endpoint,
                    CreateRequest("slow"),
                    TimeSpan.FromSeconds(5))
                .AsTask();

            await TestAwaiter.WaitAsync(slowRequestEnteredTaskSource.Task, "Slow supervisor request entry", SignalWaitTimeout);

            var fastRequestTask = client.SendAsync(
                    endpoint,
                    CreateRequest("fast"),
                    TimeSpan.FromSeconds(5))
                .AsTask();
            var fastResponse = await TestAwaiter.WaitAsync(fastRequestTask, "Fast supervisor request result", SignalWaitTimeout);
            Assert.True(IpcPayloadCodec.TryDeserialize(
                fastResponse.Payload,
                out TransportServerResponse fastPayload,
                out _));
            Assert.Equal("fast", fastPayload.Method);

            releaseSlowRequestTaskSource.TrySetResult();

            var slowResponse = await TestAwaiter.WaitAsync(slowRequestTask, "Slow supervisor request result", SignalWaitTimeout);
            Assert.True(IpcPayloadCodec.TryDeserialize(
                slowResponse.Payload,
                out TransportServerResponse slowPayload,
                out _));
            Assert.Equal("slow", slowPayload.Method);
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await TestAwaiter.WaitAsync(serverTask, "Supervisor transport shutdown", SignalWaitTimeout);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public async Task Run_OnUnix_WhenSocketDirectoryCannotBeSecured_ThrowsIOException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("supervisor-transport-server", "blocked-socket-directory");
        var blockedDirectoryPath = scope.WriteFile("blocked", "directory path is blocked");
        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            Path.Combine(blockedDirectoryPath, UcliIpcEndpointNames.UnixSocketFileName));
        var server = new SupervisorTransportServer();

        var exception = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                server.RunAsync(
                    endpoint,
                    static (_, _) => Task.CompletedTask,
                    static _ => Task.CompletedTask,
                    CancellationToken.None),
                "Blocked socket directory server start",
                SignalWaitTimeout);
        });

        Assert.Contains(blockedDirectoryPath, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public async Task Run_OnUnix_AppliesOwnerOnlyPermissionsToSocketAndParentDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            UnixSocketPathUtilities.BuildFallbackSocketPath("ucli-supervisor-", Guid.NewGuid().ToString("N")));
        var server = new SupervisorTransportServer();
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            endpoint,
            static (_, _) => Task.CompletedTask,
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            cancellationTokenSource.Token);

        try
        {
            await TestAwaiter.WaitAsync(startedTaskSource.Task, "Unix supervisor socket start", SignalWaitTimeout);

            PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(Path.GetDirectoryName(endpoint.Address)!);
            PosixAccessBoundaryAssert.FileIsOwnerOnly(endpoint.Address);
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await TestAwaiter.WaitAsync(serverTask, "Unix supervisor socket shutdown", SignalWaitTimeout);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public async Task Run_OnUnix_WhenUsingFallbackEndpoint_DeletesEmptyFallbackDirectoryOnShutdown ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            UnixSocketPathUtilities.BuildFallbackSocketPath("ucli-supervisor-", Guid.NewGuid().ToString("N")));
        var socketDirectoryPath = Path.GetDirectoryName(endpoint.Address)!;
        var server = new SupervisorTransportServer();
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            endpoint,
            static (_, _) => Task.CompletedTask,
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            cancellationTokenSource.Token);

        try
        {
            await TestAwaiter.WaitAsync(startedTaskSource.Task, "Unix supervisor fallback socket start", SignalWaitTimeout);
            Assert.True(Directory.Exists(socketDirectoryPath));
            Assert.True(File.Exists(endpoint.Address));
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await TestAwaiter.WaitAsync(serverTask, "Unix supervisor fallback socket shutdown", SignalWaitTimeout);
            }
            catch (OperationCanceledException)
            {
            }
        }

        Assert.False(File.Exists(endpoint.Address));
        Assert.False(Directory.Exists(socketDirectoryPath));
    }

    private static IpcEndpoint CreateEndpoint (string storageRoot)
    {
        if (OperatingSystem.IsWindows())
        {
            return new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                $"{UcliIpcEndpointNames.SupervisorAddressPrefix}transport-{Guid.NewGuid():N}");
        }

        return new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            UnixSocketPathUtilities.BuildFallbackSocketPath(
                UcliIpcEndpointNames.SupervisorAddressPrefix + "transport-",
                storageRoot));
    }

    private static IpcRequest CreateRequest (string method)
    {
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"request-{Guid.NewGuid():N}",
            SessionToken: "session-token",
            Method: method,
            Payload: IpcPayloadCodec.SerializeToElement(new { }),
            responseMode: IpcResponseMode.Single);
    }

    private sealed record TransportServerResponse (string Method);
}
