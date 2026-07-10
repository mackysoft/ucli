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
    [Trait("Size", "Medium")]
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
            SupervisorConstants.MaximumActiveConnections,
            SupervisorConstants.ConnectionDrainTimeout,
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
    [Trait("Size", "Medium")]
    public async Task Run_WhenConnectionHandlerBlocksBeforeReturningTask_StillAcceptsAnotherConnection ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-transport-server", "synchronous-handler-block");
        var endpoint = CreateEndpoint(scope.FullPath);
        var server = new SupervisorTransportServer();
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstHandlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();
        var handlerCallCount = 0;

        var serverTask = server.RunAsync(
            endpoint,
            (stream, cancellationToken) =>
            {
                if (Interlocked.Increment(ref handlerCallCount) == 1)
                {
                    firstHandlerEntered.TrySetResult();
                    releaseFirstHandler.Task.GetAwaiter().GetResult();
                }

                return EchoRequestAsync(stream, cancellationToken);
            },
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            maximumActiveConnections: 2,
            connectionDrainTimeout: SupervisorConstants.ConnectionDrainTimeout,
            cancellationToken: cancellationTokenSource.Token);

        var client = new IpcTransportClient();
        Task<IpcResponse>? firstRequestTask = null;
        Task<IpcResponse>? secondRequestTask = null;
        try
        {
            await TestAwaiter.WaitAsync(startedTaskSource.Task, "Supervisor transport start", SignalWaitTimeout);
            firstRequestTask = client
                .SendAsync(endpoint, CreateRequest("first"), SignalWaitTimeout)
                .AsTask();
            await TestAwaiter.WaitAsync(firstHandlerEntered.Task, "Synchronous supervisor handler entry", SignalWaitTimeout);

            secondRequestTask = client
                .SendAsync(endpoint, CreateRequest("second"), SignalWaitTimeout)
                .AsTask();
            var secondResponse = await TestAwaiter.WaitAsync(
                secondRequestTask,
                "Second supervisor response",
                SignalWaitTimeout);

            Assert.Equal(IpcProtocol.StatusOk, secondResponse.Status);
            Assert.Equal(2, Volatile.Read(ref handlerCallCount));

            releaseFirstHandler.TrySetResult();
            var firstResponse = await TestAwaiter.WaitAsync(
                firstRequestTask,
                "First supervisor response",
                SignalWaitTimeout);
            Assert.Equal(IpcProtocol.StatusOk, firstResponse.Status);
        }
        finally
        {
            releaseFirstHandler.TrySetResult();
            cancellationTokenSource.Cancel();
            server.Release();
            await TestAwaiter.WaitAsync(serverTask, "Supervisor transport shutdown", SignalWaitTimeout);
            if (firstRequestTask is not null)
            {
                await ObserveConnectionCompletionAsync(firstRequestTask);
            }

            if (secondRequestTask is not null)
            {
                await ObserveConnectionCompletionAsync(secondRequestTask);
            }
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public async Task Run_OnUnix_WhenReleasedGenerationFinishesAfterSuccessorStarts_PreservesSuccessorSocket ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("supervisor-transport-server", "successor-socket-ownership");
        var endpoint = CreateEndpoint(scope.FullPath);
        var originalServer = new SupervisorTransportServer();
        var successorServer = new SupervisorTransportServer();
        var originalBound = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOriginalPublication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var successorStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var originalCancellationTokenSource = new CancellationTokenSource();
        using var successorCancellationTokenSource = new CancellationTokenSource();

        var originalServerTask = originalServer.RunAsync(
            endpoint,
            EchoRequestAsync,
            async _ =>
            {
                originalBound.TrySetResult();
                await releaseOriginalPublication.Task.ConfigureAwait(false);
            },
            SupervisorConstants.MaximumActiveConnections,
            SupervisorConstants.ConnectionDrainTimeout,
            originalCancellationTokenSource.Token);
        var successorServerTask = Task.CompletedTask;

        try
        {
            await TestAwaiter.WaitAsync(originalBound.Task, "Original supervisor socket bind", SignalWaitTimeout);
            successorServerTask = successorServer.RunAsync(
                endpoint,
                EchoRequestAsync,
                _ =>
                {
                    successorStarted.TrySetResult();
                    return Task.CompletedTask;
                },
                SupervisorConstants.MaximumActiveConnections,
                SupervisorConstants.ConnectionDrainTimeout,
                successorCancellationTokenSource.Token);
            await TestAwaiter.WaitAsync(successorStarted.Task, "Successor supervisor socket start", SignalWaitTimeout);
            Assert.True(File.Exists(endpoint.Address));

            originalCancellationTokenSource.Cancel();
            originalServer.Release();
            releaseOriginalPublication.TrySetResult();
            await TestAwaiter.WaitAsync(originalServerTask, "Original supervisor transport shutdown", SignalWaitTimeout);

            Assert.True(File.Exists(endpoint.Address));
            var response = await new IpcTransportClient().SendAsync(
                endpoint,
                CreateRequest("successor"),
                SignalWaitTimeout);
            Assert.Equal(IpcProtocol.StatusOk, response.Status);
        }
        finally
        {
            releaseOriginalPublication.TrySetResult();
            originalCancellationTokenSource.Cancel();
            originalServer.Release();
            successorCancellationTokenSource.Cancel();
            successorServer.Release();
            await ObserveConnectionCompletionAsync(originalServerTask);
            await ObserveConnectionCompletionAsync(successorServerTask);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public async Task Run_OnUnix_WhenSuccessorStartupFails_RestoresPreviousPublishedGeneration ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            UnixSocketPathUtilities.BuildFallbackSocketPath(
                UcliIpcEndpointNames.SupervisorAddressPrefix,
                Guid.NewGuid().ToString("N")));
        var originalServer = new SupervisorTransportServer();
        var successorServer = new SupervisorTransportServer();
        var originalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var originalCancellationTokenSource = new CancellationTokenSource();
        var originalTask = originalServer.RunAsync(
            endpoint,
            EchoRequestAsync,
            _ =>
            {
                originalStarted.TrySetResult();
                return Task.CompletedTask;
            },
            SupervisorConstants.MaximumActiveConnections,
            SupervisorConstants.ConnectionDrainTimeout,
            originalCancellationTokenSource.Token);

        try
        {
            await TestAwaiter.WaitAsync(originalStarted.Task, "Original supervisor start", SignalWaitTimeout);
            Assert.True(SupervisorUnixSocketEndpointOwnership.TryResolvePublishedGenerationAddress(
                endpoint.Address,
                out var originalGenerationAddress));

            var startupException = await Assert.ThrowsAsync<InvalidOperationException>(() => successorServer.RunAsync(
                endpoint,
                EchoRequestAsync,
                static _ => throw new InvalidOperationException("Successor manifest publication failed."),
                SupervisorConstants.MaximumActiveConnections,
                SupervisorConstants.ConnectionDrainTimeout,
                CancellationToken.None));

            Assert.Contains("manifest publication failed", startupException.Message, StringComparison.Ordinal);
            Assert.True(SupervisorUnixSocketEndpointOwnership.TryResolvePublishedGenerationAddress(
                endpoint.Address,
                out var restoredGenerationAddress));
            Assert.Equal(originalGenerationAddress, restoredGenerationAddress);
            var response = await new IpcTransportClient().SendAsync(
                endpoint,
                CreateRequest("restored-original"),
                SignalWaitTimeout);
            Assert.Equal(IpcProtocol.StatusOk, response.Status);
        }
        finally
        {
            originalCancellationTokenSource.Cancel();
            originalServer.Release();
            successorServer.Release();
            await ObserveConnectionCompletionAsync(originalTask);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public async Task Run_OnUnix_WhenStartupIsCanceled_RemovesUncommittedPublicationAndGenerationNode ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            UnixSocketPathUtilities.BuildFallbackSocketPath(
                UcliIpcEndpointNames.SupervisorAddressPrefix,
                Guid.NewGuid().ToString("N")));
        var server = new SupervisorTransportServer();
        using var cancellationTokenSource = new CancellationTokenSource();
        var generationAddress = string.Empty;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.RunAsync(
            endpoint,
            EchoRequestAsync,
            cancellationToken =>
            {
                Assert.True(SupervisorUnixSocketEndpointOwnership.TryResolvePublishedGenerationAddress(
                    endpoint.Address,
                    out generationAddress));
                cancellationTokenSource.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            SupervisorConstants.MaximumActiveConnections,
            SupervisorConstants.ConnectionDrainTimeout,
            cancellationTokenSource.Token));

        Assert.Null(new FileInfo(endpoint.Address).LinkTarget);
        Assert.False(File.Exists(generationAddress));
        Assert.False(Directory.Exists(Path.GetDirectoryName(generationAddress)));
    }

    [Fact]
    [Trait("Size", "Medium")]
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
                    SupervisorConstants.MaximumActiveConnections,
                    SupervisorConstants.ConnectionDrainTimeout,
                    CancellationToken.None),
                "Blocked socket directory server start",
                SignalWaitTimeout);
        });

        Assert.Contains(blockedDirectoryPath, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
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
            SupervisorConstants.MaximumActiveConnections,
            SupervisorConstants.ConnectionDrainTimeout,
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
    [Trait("Size", "Medium")]
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
            SupervisorConstants.MaximumActiveConnections,
            SupervisorConstants.ConnectionDrainTimeout,
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

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Run_WhenActiveConnectionLimitIsReached_RejectsExcessConnectionWithoutInvokingHandler ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-transport-server", "connection-limit");
        var endpoint = CreateEndpoint(scope.FullPath);
        var server = new SupervisorTransportServer();
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstHandlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();
        var handlerCallCount = 0;

        var serverTask = server.RunAsync(
            endpoint,
            async (stream, cancellationToken) =>
            {
                Interlocked.Increment(ref handlerCallCount);
                var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequest>(
                        stream,
                        IpcJsonSerializerOptions.Default,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                Assert.True(readResult.IsSuccess);
                firstHandlerEntered.TrySetResult();
                await releaseFirstHandler.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

                var request = readResult.Value;
                await IpcFrameCodec.WriteModelAsync(
                        stream,
                        new IpcResponse(
                            ProtocolVersion: request.ProtocolVersion,
                            RequestId: request.RequestId,
                            Status: IpcProtocol.StatusOk,
                            Payload: IpcPayloadCodec.SerializeToElement(new TransportServerResponse(request.Method)),
                            Errors: Array.Empty<IpcError>()),
                        IpcJsonSerializerOptions.Default,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            },
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            maximumActiveConnections: 1,
            connectionDrainTimeout: SupervisorConstants.ConnectionDrainTimeout,
            cancellationToken: cancellationTokenSource.Token);

        var client = new IpcTransportClient();
        Task<IpcResponse>? firstRequestTask = null;
        try
        {
            await TestAwaiter.WaitAsync(startedTaskSource.Task, "Supervisor transport start", SignalWaitTimeout);
            firstRequestTask = client
                .SendAsync(endpoint, CreateRequest("first"), SignalWaitTimeout)
                .AsTask();
            await TestAwaiter.WaitAsync(firstHandlerEntered.Task, "First supervisor connection", SignalWaitTimeout);

            var secondRequestTask = client
                .SendAsync(endpoint, CreateRequest("overflow"), SignalWaitTimeout)
                .AsTask();
            var rejectionException = await Record.ExceptionAsync(async () =>
                await TestAwaiter.WaitAsync(
                    secondRequestTask,
                    "Rejected supervisor connection",
                    SignalWaitTimeout));

            Assert.IsAssignableFrom<IOException>(rejectionException);
            Assert.Equal(1, Volatile.Read(ref handlerCallCount));
            releaseFirstHandler.TrySetResult();
            var firstResponse = await TestAwaiter.WaitAsync(
                firstRequestTask,
                "First supervisor response",
                SignalWaitTimeout);
            Assert.Equal(IpcProtocol.StatusOk, firstResponse.Status);
        }
        finally
        {
            releaseFirstHandler.TrySetResult();
            cancellationTokenSource.Cancel();
            server.Release();
            await TestAwaiter.WaitAsync(serverTask, "Supervisor transport shutdown", SignalWaitTimeout);
            if (firstRequestTask is not null)
            {
                await ObserveConnectionCompletionAsync(firstRequestTask);
            }
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Run_WhenConnectionHandlerIgnoresShutdownCancellation_ReturnsAtConnectionDrainDeadline ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-transport-server", "bounded-drain");
        var endpoint = CreateEndpoint(scope.FullPath);
        var server = new SupervisorTransportServer();
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            endpoint,
            async (_, _) =>
            {
                handlerEntered.TrySetResult();
                await releaseHandler.Task.ConfigureAwait(false);
            },
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            maximumActiveConnections: 1,
            connectionDrainTimeout: TimeSpan.FromMilliseconds(50),
            cancellationToken: cancellationTokenSource.Token);

        var client = new IpcTransportClient();
        var requestTask = Task.CompletedTask;
        var returnedAtDrainDeadline = false;
        try
        {
            await TestAwaiter.WaitAsync(startedTaskSource.Task, "Supervisor transport start", SignalWaitTimeout);
            requestTask = client
                .SendAsync(endpoint, CreateRequest("non-cooperative"), SignalWaitTimeout)
                .AsTask();
            await TestAwaiter.WaitAsync(handlerEntered.Task, "Non-cooperative supervisor handler", SignalWaitTimeout);

            cancellationTokenSource.Cancel();
            server.Release();
            await serverTask.WaitAsync(TimeSpan.FromMilliseconds(500));
            returnedAtDrainDeadline = true;
        }
        finally
        {
            releaseHandler.TrySetResult();
            cancellationTokenSource.Cancel();
            server.Release();
            await TestAwaiter.WaitAsync(serverTask, "Supervisor bounded-drain shutdown", SignalWaitTimeout);
            await ObserveConnectionCompletionAsync(requestTask);
        }

        Assert.True(returnedAtDrainDeadline);
    }

    private static async Task ObserveConnectionCompletionAsync (Task task)
    {
        try
        {
            await task.WaitAsync(SignalWaitTimeout);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException or TimeoutException)
        {
        }
    }

    private static async Task EchoRequestAsync (
        Stream stream,
        CancellationToken cancellationToken)
    {
        var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequest>(
                stream,
                IpcJsonSerializerOptions.Default,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        Assert.True(readResult.IsSuccess);

        var request = readResult.Value;
        await IpcFrameCodec.WriteModelAsync(
                stream,
                new IpcResponse(
                    ProtocolVersion: request.ProtocolVersion,
                    RequestId: request.RequestId,
                    Status: IpcProtocol.StatusOk,
                    Payload: IpcPayloadCodec.SerializeToElement(new TransportServerResponse(request.Method)),
                    Errors: Array.Empty<IpcError>()),
                IpcJsonSerializerOptions.Default,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
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
