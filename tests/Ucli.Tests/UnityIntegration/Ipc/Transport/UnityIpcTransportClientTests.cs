using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcTransportClientTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan SendWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenNamedPipeServerIsMissing_ThrowsConnectTimeoutException ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var endpointResolver = new FixedEndpointResolver(
            new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                $"ucli-missing-{Guid.NewGuid():N}"));
        var client = new UnityIpcTransportClient(endpointResolver, new IpcTransportClient());
        var request = new IpcRequest(
            IpcProtocol.CurrentVersion,
            "request-1",
            "token",
            IpcMethodNames.Ping,
            JsonDocument.Parse("{}").RootElement.Clone());

        var exception = await Assert.ThrowsAsync<IpcConnectTimeoutException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                client.SendAsync("storage-root", "fingerprint", request, DefaultTimeout).AsTask(),
                "Missing named pipe send result",
                SendWaitTimeout);
        });
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var endpointResolver = new FixedEndpointResolver(
            new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                $"ucli-missing-{Guid.NewGuid():N}"));
        var client = new UnityIpcTransportClient(endpointResolver, new IpcTransportClient());
        var request = new IpcRequest(
            IpcProtocol.CurrentVersion,
            "request-1",
            "token",
            IpcMethodNames.Ping,
            JsonDocument.Parse("{}").RootElement.Clone());
        using var cancellationTokenSource = new CancellationTokenSource();

        var sendTask = client.SendAsync(
                "storage-root",
                "fingerprint",
                request,
                TimeSpan.FromSeconds(5),
                cancellationTokenSource.Token)
            .AsTask();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(sendTask, "Canceled Unity IPC send", SendWaitTimeout);
        });
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendAsync_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException (int timeoutMilliseconds)
    {
        var endpointResolver = new FixedEndpointResolver(
            new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                $"ucli-invalid-timeout-{Guid.NewGuid():N}"));
        var client = new UnityIpcTransportClient(endpointResolver, new IpcTransportClient());
        var request = new IpcRequest(
            IpcProtocol.CurrentVersion,
            "request-1",
            "token",
            IpcMethodNames.Ping,
            JsonDocument.Parse("{}").RootElement.Clone());
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                client.SendAsync("storage-root", "fingerprint", request, timeout).AsTask(),
                "Invalid timeout send result",
                SendWaitTimeout);
        });
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("protocol")]
    [InlineData("requestId")]
    [InlineData("status")]
    [InlineData("errors")]
    public async Task SendAsync_WhenResponseEnvelopeIsInvalid_ThrowsInvalidDataException (string invalidField)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await WithUnixResponseServerAsync(
            request => invalidField switch
            {
                "protocol" => CreateResponse(request.RequestId, "{}", protocolVersion: IpcProtocol.CurrentVersion + 1),
                "requestId" => CreateResponse("other-request", "{}"),
                "status" => CreateResponse(request.RequestId, "{}", status: "unknown"),
                "errors" => new IpcResponse(
                    IpcProtocol.CurrentVersion,
                    request.RequestId,
                    IpcProtocol.StatusOk,
                    Json("{}"),
                    null!),
                _ => throw new InvalidOperationException("Unsupported invalid field."),
            },
            async (client, request) =>
            {
                await Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await client.SendAsync(
                            "storage-root",
                            "fingerprint",
                            request,
                            DefaultTimeout)
                        .AsTask();
                });
            });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenServerReturnsProgressThenTerminal_ForwardsProgressAndReturnsResponse ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcStreamFrameKinds.Progress,
                        "test.progress",
                        Json("""{"progress":true}"""),
                        Response: null),
                    cancellationToken);
                await WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcStreamFrameKinds.Terminal,
                        Event: null,
                        Json("{}"),
                        CreateResponse(request.RequestId, """{"done":true}""")),
                    cancellationToken);
            },
            async (client, request) =>
            {
                var progressFrames = new List<IpcStreamFrame>();

                var response = await client.SendStreamingAsync(
                    "storage-root",
                    "fingerprint",
                    request,
                    DefaultTimeout,
                    (frame, _) =>
                    {
                        progressFrames.Add(frame);
                        return ValueTask.CompletedTask;
                    });

                var progressFrame = Assert.Single(progressFrames);
                Assert.Equal("test.progress", progressFrame.Event);
                Assert.True(progressFrame.Payload.GetProperty("progress").GetBoolean());
                Assert.Equal("request-1", response.RequestId);
                Assert.True(response.Payload.GetProperty("done").GetBoolean());
            });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenProgressCallbackThrows_ThrowsProgressFrameHandlerException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var handlerException = new InvalidOperationException("callback failed");
        await WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcStreamFrameKinds.Progress,
                        "test.progress",
                        Json("{}"),
                        Response: null),
                    cancellationToken);
            },
            async (client, request) =>
            {
                var exception = await Assert.ThrowsAsync<IpcProgressFrameHandlerException>(async () =>
                {
                    await client.SendStreamingAsync(
                            "storage-root",
                            "fingerprint",
                            request,
                            DefaultTimeout,
                            (_, _) => throw handlerException)
                        .AsTask();
                });
                Assert.Same(handlerException, exception.HandlerException);
            });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenProgressRequestIdMismatches_ThrowsInvalidDataException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await WithUnixStreamingServerAsync(
            async (_, stream, cancellationToken) =>
            {
                await WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        "other-request",
                        IpcStreamFrameKinds.Progress,
                        "test.progress",
                        Json("{}"),
                        Response: null),
                    cancellationToken);
            },
            async (client, request) =>
            {
                await Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await client.SendStreamingAsync(
                            "storage-root",
                            "fingerprint",
                            request,
                            DefaultTimeout,
                            (_, _) => ValueTask.CompletedTask)
                        .AsTask();
                });
            });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenFrameKindIsUnsupported_ThrowsInvalidDataException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        "unsupported",
                        "test.progress",
                        Json("{}"),
                        Response: null),
                    cancellationToken);
            },
            async (client, request) =>
            {
                await Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await client.SendStreamingAsync(
                            "storage-root",
                            "fingerprint",
                            request,
                            DefaultTimeout,
                            (_, _) => ValueTask.CompletedTask)
                        .AsTask();
                });
            });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenProgressProtocolVersionMismatches_ThrowsInvalidDataException ()
    {
        await AssertStreamingFrameRejectedAsync(request => new IpcStreamFrame(
            IpcProtocol.CurrentVersion + 1,
            request.RequestId,
            IpcStreamFrameKinds.Progress,
            "test.progress",
            Json("{}"),
            Response: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenProgressEventIsMissing_ThrowsInvalidDataException ()
    {
        await AssertStreamingFrameRejectedAsync(request => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKinds.Progress,
            Event: null,
            Json("{}"),
            Response: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenProgressContainsResponse_ThrowsInvalidDataException ()
    {
        await AssertStreamingFrameRejectedAsync(request => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKinds.Progress,
            "test.progress",
            Json("{}"),
            CreateResponse(request.RequestId, "{}")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenTerminalResponseIsMissing_ThrowsInvalidDataException ()
    {
        await AssertStreamingFrameRejectedAsync(request => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKinds.Terminal,
            Event: null,
            Json("{}"),
            Response: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenTerminalContainsEvent_ThrowsInvalidDataException ()
    {
        await AssertStreamingFrameRejectedAsync(request => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKinds.Terminal,
            "test.progress",
            Json("{}"),
            CreateResponse(request.RequestId, "{}")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenTerminalResponseRequestIdMismatches_ThrowsInvalidDataException ()
    {
        await AssertStreamingFrameRejectedAsync(request => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKinds.Terminal,
            Event: null,
            Json("{}"),
            CreateResponse("other-request", "{}")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenTerminalResponseProtocolVersionMismatches_ThrowsInvalidDataException ()
    {
        await AssertStreamingFrameRejectedAsync(request => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKinds.Terminal,
            Event: null,
            Json("{}"),
            CreateResponse(request.RequestId, "{}", protocolVersion: IpcProtocol.CurrentVersion + 1)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenTerminalResponseStatusIsUnsupported_ThrowsInvalidDataException ()
    {
        await AssertStreamingFrameRejectedAsync(request => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKinds.Terminal,
            Event: null,
            Json("{}"),
            CreateResponse(request.RequestId, "{}", status: "unknown")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenTerminalResponseErrorsIsNull_ThrowsInvalidDataException ()
    {
        await AssertStreamingFrameRejectedAsync(request => new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKinds.Terminal,
            Event: null,
            Json("{}"),
            new IpcResponse(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcProtocol.StatusOk,
                Json("{}"),
                null!)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WithStreamResponseMode_ThrowsInvalidOperationException ()
    {
        var endpointResolver = new FixedEndpointResolver(
            new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                $"ucli-stream-mode-{Guid.NewGuid():N}"));
        var client = new UnityIpcTransportClient(endpointResolver, new IpcTransportClient());
        var request = new IpcRequest(
            IpcProtocol.CurrentVersion,
            "request-1",
            "token",
            IpcMethodNames.Ping,
            JsonDocument.Parse("{}").RootElement.Clone(),
            IpcResponseModes.Stream);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.SendAsync("storage-root", "fingerprint", request, DefaultTimeout).AsTask();
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WithSingleResponseMode_ThrowsInvalidOperationException ()
    {
        var endpointResolver = new FixedEndpointResolver(
            new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                $"ucli-single-mode-{Guid.NewGuid():N}"));
        var client = new UnityIpcTransportClient(endpointResolver, new IpcTransportClient());
        var request = new IpcRequest(
            IpcProtocol.CurrentVersion,
            "request-1",
            "token",
            IpcMethodNames.Ping,
            JsonDocument.Parse("{}").RootElement.Clone());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.SendStreamingAsync(
                    "storage-root",
                    "fingerprint",
                    request,
                    DefaultTimeout,
                    (_, _) => ValueTask.CompletedTask)
                .AsTask();
        });
    }

    private static async Task WithUnixStreamingServerAsync (
        Func<IpcRequest, Stream, CancellationToken, Task> writeFramesAsync,
        Func<UnityIpcTransportClient, IpcRequest, Task> executeClientAsync)
    {
        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            UnixSocketPathUtilities.BuildFallbackSocketPath("ucli-supervisor-", Guid.NewGuid().ToString("N")));
        var server = new SupervisorTransportServer();
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            endpoint,
            async (stream, cancellationToken) =>
            {
                var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequest>(
                    stream,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken);
                if (!readResult.IsSuccess)
                {
                    throw new InvalidDataException(readResult.ErrorMessage);
                }

                await writeFramesAsync(readResult.Value, stream, cancellationToken);
            },
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            cancellationTokenSource.Token);

        try
        {
            await TestAwaiter.WaitAsync(startedTaskSource.Task, "Unity IPC streaming transport server start", SendWaitTimeout);
            var client = new UnityIpcTransportClient(new FixedEndpointResolver(endpoint), new IpcTransportClient());
            await executeClientAsync(client, CreateStreamingRequest());
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await TestAwaiter.WaitAsync(serverTask, "Unity IPC streaming transport server shutdown", SendWaitTimeout);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task WithUnixResponseServerAsync (
        Func<IpcRequest, IpcResponse> createResponse,
        Func<UnityIpcTransportClient, IpcRequest, Task> executeClientAsync)
    {
        var endpoint = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            UnixSocketPathUtilities.BuildFallbackSocketPath("ucli-supervisor-", Guid.NewGuid().ToString("N")));
        var server = new SupervisorTransportServer();
        var startedTaskSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationTokenSource = new CancellationTokenSource();

        var serverTask = server.RunAsync(
            endpoint,
            async (stream, cancellationToken) =>
            {
                var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequest>(
                    stream,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken);
                if (!readResult.IsSuccess)
                {
                    throw new InvalidDataException(readResult.ErrorMessage);
                }

                await IpcFrameCodec.WriteModelAsync(
                    stream,
                    createResponse(readResult.Value),
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken);
            },
            cancellationToken =>
            {
                startedTaskSource.TrySetResult();
                return Task.CompletedTask;
            },
            cancellationTokenSource.Token);

        try
        {
            await TestAwaiter.WaitAsync(startedTaskSource.Task, "Unity IPC transport server start", SendWaitTimeout);
            var client = new UnityIpcTransportClient(new FixedEndpointResolver(endpoint), new IpcTransportClient());
            await executeClientAsync(client, CreateSingleRequest());
        }
        finally
        {
            cancellationTokenSource.Cancel();
            server.Release();
            try
            {
                await TestAwaiter.WaitAsync(serverTask, "Unity IPC transport server shutdown", SendWaitTimeout);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task AssertStreamingFrameRejectedAsync (Func<IpcRequest, IpcStreamFrame> createFrame)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await WriteStreamFrameAsync(stream, createFrame(request), cancellationToken);
            },
            async (client, request) =>
            {
                await Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await client.SendStreamingAsync(
                            "storage-root",
                            "fingerprint",
                            request,
                            DefaultTimeout,
                            (_, _) => ValueTask.CompletedTask)
                        .AsTask();
                });
            });
    }

    private static async Task WriteStreamFrameAsync (
        Stream stream,
        IpcStreamFrame frame,
        CancellationToken cancellationToken)
    {
        await IpcFrameCodec.WriteModelAsync(
            stream,
            frame,
            IpcJsonSerializerOptions.Default,
            cancellationToken: cancellationToken);
    }

    private static IpcRequest CreateStreamingRequest ()
    {
        return new IpcRequest(
            IpcProtocol.CurrentVersion,
            "request-1",
            "token",
            IpcMethodNames.Ping,
            Json("{}"),
            IpcResponseModes.Stream);
    }

    private static IpcRequest CreateSingleRequest ()
    {
        return new IpcRequest(
            IpcProtocol.CurrentVersion,
            "request-1",
            "token",
            IpcMethodNames.Ping,
            Json("{}"));
    }

    private static IpcResponse CreateResponse (
        string requestId,
        string payloadJson,
        int? protocolVersion = null,
        string? status = null)
    {
        return new IpcResponse(
            protocolVersion ?? IpcProtocol.CurrentVersion,
            requestId,
            status ?? IpcProtocol.StatusOk,
            Json(payloadJson),
            Array.Empty<IpcError>());
    }

    private static JsonElement Json (string json)
    {
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private sealed class FixedEndpointResolver : IIpcEndpointResolver
    {
        private readonly IpcEndpoint endpoint;

        public FixedEndpointResolver (IpcEndpoint endpoint)
        {
            this.endpoint = endpoint;
        }

        public IpcEndpoint Resolve (
            string storageRoot,
            string projectFingerprint)
        {
            return endpoint;
        }
    }
}
