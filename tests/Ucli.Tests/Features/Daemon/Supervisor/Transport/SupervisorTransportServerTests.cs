using System.Runtime.Versioning;
using MackySoft.FileSystem;
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
        var endpoint = CreateEndpoint(AbsolutePath.Parse(scope.FullPath));
        var server = new SupervisorTransportServer(TimeProvider.System);
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slowRequestEnteredTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSlowRequestTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            SupervisorTransportEndpoint.FromContract(endpoint),
            async (stream, cancellationToken) =>
            {
                var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequestEnvelope>(
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
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    status: IpcResponseStatus.Ok,
                    payload: IpcPayloadCodec.SerializeToElement(new TransportServerResponse(request.Method)),
                    errors: Array.Empty<IpcError>());
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
            await startedTaskSource.Task.WaitAsync(SignalWaitTimeout);

            var client = new IpcTransportClient(
                new IpcTransportConnector(),
                TimeProvider.System);
            var slowRequestTask = client.SendAsync(
                    IpcTransportEndpoint.FromContract(endpoint),
                    CreateRequest("slow"),
                    TimeSpan.FromSeconds(5))
                .AsTask();

            await slowRequestEnteredTaskSource.Task.WaitAsync(SignalWaitTimeout);

            var fastRequestTask = client.SendAsync(
                    IpcTransportEndpoint.FromContract(endpoint),
                    CreateRequest("fast"),
                    TimeSpan.FromSeconds(5))
                .AsTask();
            var fastResponse = await fastRequestTask.WaitAsync(SignalWaitTimeout);
            Assert.True(IpcPayloadCodec.TryDeserialize(
                fastResponse.Payload,
                out TransportServerResponse fastPayload,
                out _));
            Assert.Equal("fast", fastPayload.Method);

            releaseSlowRequestTaskSource.TrySetResult();

            var slowResponse = await slowRequestTask.WaitAsync(SignalWaitTimeout);
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
                await serverTask.WaitAsync(SignalWaitTimeout);
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
        var endpoint = CreateEndpoint(AbsolutePath.Parse(scope.FullPath));
        var server = new SupervisorTransportServer(TimeProvider.System);
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstHandlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();
        var handlerCallCount = 0;

        var serverTask = server.RunAsync(
            SupervisorTransportEndpoint.FromContract(endpoint),
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

        var client = new IpcTransportClient(
            new IpcTransportConnector(),
            TimeProvider.System);
        Task<IpcResponse>? firstRequestTask = null;
        Task<IpcResponse>? secondRequestTask = null;
        try
        {
            await startedTaskSource.Task.WaitAsync(SignalWaitTimeout);
            firstRequestTask = client
                .SendAsync(IpcTransportEndpoint.FromContract(endpoint), CreateRequest("first"), SignalWaitTimeout)
                .AsTask();
            await firstHandlerEntered.Task.WaitAsync(SignalWaitTimeout);

            secondRequestTask = client
                .SendAsync(IpcTransportEndpoint.FromContract(endpoint), CreateRequest("second"), SignalWaitTimeout)
                .AsTask();
            var secondResponse = await secondRequestTask.WaitAsync(SignalWaitTimeout);

            Assert.Equal(IpcResponseStatus.Ok, secondResponse.Status);
            Assert.Equal(2, Volatile.Read(ref handlerCallCount));

            releaseFirstHandler.TrySetResult();
            var firstResponse = await firstRequestTask.WaitAsync(SignalWaitTimeout);
            Assert.Equal(IpcResponseStatus.Ok, firstResponse.Status);
        }
        finally
        {
            releaseFirstHandler.TrySetResult();
            cancellationTokenSource.Cancel();
            server.Release();
            await serverTask.WaitAsync(SignalWaitTimeout);
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
    public async Task Run_OnNamedPipe_WhenStartupCallbackThrowsIOException_PropagatesWithoutRetry ()
    {
        var endpoint = new IpcEndpoint(
            IpcTransportKind.NamedPipe,
            UcliIpcEndpointNames.SupervisorAddressPrefix + Guid.NewGuid().ToString("N")[..16]);
        var server = new SupervisorTransportServer(TimeProvider.System);
        using var cancellationTokenSource = new CancellationTokenSource();
        var startupCallbackInvocations = 0;
        var serverTask = server.RunAsync(
            SupervisorTransportEndpoint.FromContract(endpoint),
            static (_, _) => Task.CompletedTask,
            async _ =>
            {
                Interlocked.Increment(ref startupCallbackInvocations);
                await Task.Yield();
                throw new IOException("Supervisor startup callback failed.");
            },
            SupervisorConstants.MaximumActiveConnections,
            SupervisorConstants.ConnectionDrainTimeout,
            cancellationTokenSource.Token);

        try
        {
            var exception = await Assert.ThrowsAsync<IOException>(async () =>
                await serverTask.WaitAsync(SignalWaitTimeout));

            Assert.Contains("startup callback failed", exception.Message, StringComparison.Ordinal);
            Assert.Equal(1, Volatile.Read(ref startupCallbackInvocations));
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            await ObserveConnectionCompletionAsync(serverTask);
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
        var endpoint = CreateEndpoint(AbsolutePath.Parse(scope.FullPath));
        var originalServer = new SupervisorTransportServer(TimeProvider.System);
        var successorServer = new SupervisorTransportServer(TimeProvider.System);
        var originalBound = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOriginalPublication = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var successorStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var originalCancellationTokenSource = new CancellationTokenSource();
        using var successorCancellationTokenSource = new CancellationTokenSource();

        var originalServerTask = originalServer.RunAsync(
            SupervisorTransportEndpoint.FromContract(endpoint),
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
            await originalBound.Task.WaitAsync(SignalWaitTimeout);
            successorServerTask = successorServer.RunAsync(
                SupervisorTransportEndpoint.FromContract(endpoint),
                EchoRequestAsync,
                _ =>
                {
                    successorStarted.TrySetResult();
                    return Task.CompletedTask;
                },
                SupervisorConstants.MaximumActiveConnections,
                SupervisorConstants.ConnectionDrainTimeout,
                successorCancellationTokenSource.Token);
            await successorStarted.Task.WaitAsync(SignalWaitTimeout);
            Assert.True(File.Exists(endpoint.Address));

            originalCancellationTokenSource.Cancel();
            originalServer.Release();
            releaseOriginalPublication.TrySetResult();
            await originalServerTask.WaitAsync(SignalWaitTimeout);

            Assert.True(File.Exists(endpoint.Address));
            var response = await new IpcTransportClient(
                    new IpcTransportConnector(),
                    TimeProvider.System)
                .SendAsync(
                IpcTransportEndpoint.FromContract(endpoint),
                CreateRequest("successor"),
                SignalWaitTimeout);
            Assert.Equal(IpcResponseStatus.Ok, response.Status);
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
            new UnixSocketFallbackPath(
                AbsolutePath.Parse(Path.GetTempPath()),
                UnixSocketFallbackPurpose.Supervisor,
                Guid.NewGuid().ToString("N")).SocketPath.Value);
        var originalServer = new SupervisorTransportServer(TimeProvider.System);
        var successorServer = new SupervisorTransportServer(TimeProvider.System);
        var originalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var originalCancellationTokenSource = new CancellationTokenSource();
        var originalTask = originalServer.RunAsync(
            SupervisorTransportEndpoint.FromContract(endpoint),
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
            await originalStarted.Task.WaitAsync(SignalWaitTimeout);
            Assert.True(SupervisorUnixSocketEndpointOwnership.TryResolvePublishedGenerationAddress(
                AbsolutePath.Parse(endpoint.Address),
                out var originalGenerationAddress));

            var startupException = await Assert.ThrowsAsync<InvalidOperationException>(() => successorServer.RunAsync(
                SupervisorTransportEndpoint.FromContract(endpoint),
                EchoRequestAsync,
                static _ => throw new InvalidOperationException("Successor manifest publication failed."),
                SupervisorConstants.MaximumActiveConnections,
                SupervisorConstants.ConnectionDrainTimeout,
                CancellationToken.None));

            Assert.Contains("manifest publication failed", startupException.Message, StringComparison.Ordinal);
            Assert.True(SupervisorUnixSocketEndpointOwnership.TryResolvePublishedGenerationAddress(
                AbsolutePath.Parse(endpoint.Address),
                out var restoredGenerationAddress));
            Assert.Equal(originalGenerationAddress, restoredGenerationAddress);
            var response = await new IpcTransportClient(
                    new IpcTransportConnector(),
                    TimeProvider.System)
                .SendAsync(
                IpcTransportEndpoint.FromContract(endpoint),
                CreateRequest("restored-original"),
                SignalWaitTimeout);
            Assert.Equal(IpcResponseStatus.Ok, response.Status);
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
            new UnixSocketFallbackPath(
                AbsolutePath.Parse(Path.GetTempPath()),
                UnixSocketFallbackPurpose.Supervisor,
                Guid.NewGuid().ToString("N")).SocketPath.Value);
        var server = new SupervisorTransportServer(TimeProvider.System);
        using var cancellationTokenSource = new CancellationTokenSource();
        AbsolutePath? generationAddress = null;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.RunAsync(
            SupervisorTransportEndpoint.FromContract(endpoint),
            EchoRequestAsync,
            cancellationToken =>
            {
                Assert.True(SupervisorUnixSocketEndpointOwnership.TryResolvePublishedGenerationAddress(
                    AbsolutePath.Parse(endpoint.Address),
                    out generationAddress));
                cancellationTokenSource.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            SupervisorConstants.MaximumActiveConnections,
            SupervisorConstants.ConnectionDrainTimeout,
            cancellationTokenSource.Token));

        Assert.NotNull(generationAddress);
        Assert.Null(new FileInfo(endpoint.Address).LinkTarget);
        Assert.False(File.Exists(generationAddress.Value));
        Assert.False(Directory.Exists(Path.GetDirectoryName(generationAddress.Value)));
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

        var blockedDirectoryPath = Path.Combine("/tmp", $"ucli-blocked-{Guid.NewGuid():N}");
        File.WriteAllText(blockedDirectoryPath, "directory path is blocked");
        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            Path.Combine(blockedDirectoryPath, UcliIpcEndpointNames.UnixSocketFileName));
        var server = new SupervisorTransportServer(TimeProvider.System);

        try
        {
            var exception = await Assert.ThrowsAsync<IOException>(async () =>
            {
                await server.RunAsync(
                        SupervisorTransportEndpoint.FromContract(endpoint),
                        static (_, _) => Task.CompletedTask,
                        static _ => Task.CompletedTask,
                        SupervisorConstants.MaximumActiveConnections,
                        SupervisorConstants.ConnectionDrainTimeout,
                        CancellationToken.None).WaitAsync(SignalWaitTimeout);
            });

            Assert.Contains(blockedDirectoryPath, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(blockedDirectoryPath);
        }
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
            new UnixSocketFallbackPath(
                AbsolutePath.Parse(Path.GetTempPath()),
                UnixSocketFallbackPurpose.Supervisor,
                Guid.NewGuid().ToString("N")).SocketPath.Value);
        var server = new SupervisorTransportServer(TimeProvider.System);
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            SupervisorTransportEndpoint.FromContract(endpoint),
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
            await startedTaskSource.Task.WaitAsync(SignalWaitTimeout);

            PosixAccessBoundaryAssert.DirectoryIsOwnerOnly(Path.GetDirectoryName(endpoint.Address)!);
            PosixAccessBoundaryAssert.FileIsOwnerOnly(endpoint.Address);
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await serverTask.WaitAsync(SignalWaitTimeout);
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
    public async Task Run_OnUnix_WhenUsingFallbackEndpoint_PreservesStableFallbackDirectoryOnShutdown ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            new UnixSocketFallbackPath(
                AbsolutePath.Parse(Path.GetTempPath()),
                UnixSocketFallbackPurpose.Supervisor,
                Guid.NewGuid().ToString("N")).SocketPath.Value);
        var socketDirectoryPath = Path.GetDirectoryName(endpoint.Address)!;
        var server = new SupervisorTransportServer(TimeProvider.System);
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            SupervisorTransportEndpoint.FromContract(endpoint),
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
            await startedTaskSource.Task.WaitAsync(SignalWaitTimeout);
            Assert.True(Directory.Exists(socketDirectoryPath));
            Assert.True(File.Exists(endpoint.Address));
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await serverTask.WaitAsync(SignalWaitTimeout);
            }
            catch (OperationCanceledException)
            {
            }
        }

        Assert.False(File.Exists(endpoint.Address));
        try
        {
            Assert.True(Directory.Exists(socketDirectoryPath));
        }
        finally
        {
            if (Directory.Exists(socketDirectoryPath))
            {
                Directory.Delete(socketDirectoryPath, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Run_WhenActiveConnectionLimitIsReached_RejectsExcessConnectionWithoutInvokingHandler ()
    {
        using var scope = TestDirectories.CreateTempScope("supervisor-transport-server", "connection-limit");
        var endpoint = CreateEndpoint(AbsolutePath.Parse(scope.FullPath));
        var server = new SupervisorTransportServer(TimeProvider.System);
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstHandlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();
        var handlerCallCount = 0;

        var serverTask = server.RunAsync(
            SupervisorTransportEndpoint.FromContract(endpoint),
            async (stream, cancellationToken) =>
            {
                Interlocked.Increment(ref handlerCallCount);
                var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequestEnvelope>(
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
                            protocolVersion: request.ProtocolVersion,
                            requestId: request.RequestId,
                            status: IpcResponseStatus.Ok,
                            payload: IpcPayloadCodec.SerializeToElement(new TransportServerResponse(request.Method)),
                            errors: Array.Empty<IpcError>()),
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

        var client = new IpcTransportClient(
            new IpcTransportConnector(),
            TimeProvider.System);
        Task<IpcResponse>? firstRequestTask = null;
        try
        {
            await startedTaskSource.Task.WaitAsync(SignalWaitTimeout);
            firstRequestTask = client
                .SendAsync(IpcTransportEndpoint.FromContract(endpoint), CreateRequest("first"), SignalWaitTimeout)
                .AsTask();
            await firstHandlerEntered.Task.WaitAsync(SignalWaitTimeout);

            var secondRequestTask = client
                .SendAsync(IpcTransportEndpoint.FromContract(endpoint), CreateRequest("overflow"), SignalWaitTimeout)
                .AsTask();
            var rejectionException = await Record.ExceptionAsync(async () =>
                await secondRequestTask.WaitAsync(SignalWaitTimeout));

            Assert.IsAssignableFrom<IOException>(rejectionException);
            Assert.Equal(1, Volatile.Read(ref handlerCallCount));
            releaseFirstHandler.TrySetResult();
            var firstResponse = await firstRequestTask.WaitAsync(SignalWaitTimeout);
            Assert.Equal(IpcResponseStatus.Ok, firstResponse.Status);
        }
        finally
        {
            releaseFirstHandler.TrySetResult();
            cancellationTokenSource.Cancel();
            server.Release();
            await serverTask.WaitAsync(SignalWaitTimeout);
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
        var endpoint = CreateEndpoint(AbsolutePath.Parse(scope.FullPath));
        var timeProvider = new ManualTimeProvider();
        var server = new SupervisorTransportServer(timeProvider);
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlerEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            SupervisorTransportEndpoint.FromContract(endpoint),
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

        var client = new IpcTransportClient(
            new IpcTransportConnector(),
            TimeProvider.System);
        var requestTask = Task.CompletedTask;
        var returnedAtDrainDeadline = false;
        try
        {
            await startedTaskSource.Task.WaitAsync(SignalWaitTimeout);
            requestTask = client
                .SendAsync(IpcTransportEndpoint.FromContract(endpoint), CreateRequest("non-cooperative"), SignalWaitTimeout)
                .AsTask();
            await handlerEntered.Task.WaitAsync(SignalWaitTimeout);

            cancellationTokenSource.Cancel();
            server.Release();
            await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(50)).WaitAsync(SignalWaitTimeout);
            timeProvider.Advance(TimeSpan.FromMilliseconds(50));
            await serverTask.WaitAsync(SignalWaitTimeout);
            returnedAtDrainDeadline = true;
        }
        finally
        {
            releaseHandler.TrySetResult();
            cancellationTokenSource.Cancel();
            server.Release();
            await serverTask.WaitAsync(SignalWaitTimeout);
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
        var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequestEnvelope>(
                stream,
                IpcJsonSerializerOptions.Default,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        Assert.True(readResult.IsSuccess);

        var request = readResult.Value;
        await IpcFrameCodec.WriteModelAsync(
                stream,
                new IpcResponse(
                    protocolVersion: request.ProtocolVersion,
                    requestId: request.RequestId,
                    status: IpcResponseStatus.Ok,
                    payload: IpcPayloadCodec.SerializeToElement(new TransportServerResponse(request.Method)),
                    errors: Array.Empty<IpcError>()),
                IpcJsonSerializerOptions.Default,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static IpcEndpoint CreateEndpoint (AbsolutePath storageRoot)
    {
        if (OperatingSystem.IsWindows())
        {
            return new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                $"{UcliIpcEndpointNames.SupervisorAddressPrefix}transport-{Guid.NewGuid():N}");
        }

        return new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            new UnixSocketFallbackPath(
                AbsolutePath.Parse(Path.GetTempPath()),
                UnixSocketFallbackPurpose.Supervisor,
                storageRoot.Value).SocketPath.Value);
    }

    private static IpcRequestEnvelope CreateRequest (string method)
    {
        return new IpcRequestEnvelope(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: "session-token",
            method: method,
            payload: IpcPayloadCodec.SerializeToElement(new { }),
            responseMode: TextVocabulary.GetText(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue);
    }

    private sealed record TransportServerResponse (string? Method);
}
