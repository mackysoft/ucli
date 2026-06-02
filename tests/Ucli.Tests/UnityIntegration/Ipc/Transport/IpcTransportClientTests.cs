using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan DelayedTerminalFrameWait = TimeSpan.FromMilliseconds(1200);

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenNamedPipeServerIsMissing_ThrowsConnectTimeoutException ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var endpoint = new IpcEndpoint(
            IpcTransportKind.NamedPipe,
            $"ucli-missing-{Guid.NewGuid():N}");
        var client = new IpcTransportClient();

        var exception = await Assert.ThrowsAsync<IpcConnectTimeoutException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                client.SendAsync(endpoint, IpcTransportTestHarness.CreateSingleRequest(), DefaultTimeout).AsTask(),
                "Missing named pipe send result",
                WaitTimeout);
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

        var endpoint = new IpcEndpoint(
            IpcTransportKind.NamedPipe,
            $"ucli-missing-{Guid.NewGuid():N}");
        var client = new IpcTransportClient();
        using var cancellationTokenSource = new CancellationTokenSource();

        var sendTask = client.SendAsync(
                endpoint,
                IpcTransportTestHarness.CreateSingleRequest(),
                TimeSpan.FromSeconds(5),
                cancellationTokenSource.Token)
            .AsTask();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(sendTask, "Canceled IPC send", WaitTimeout);
        });
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendAsync_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException (int timeoutMilliseconds)
    {
        var endpoint = new IpcEndpoint(
            IpcTransportKind.NamedPipe,
            $"ucli-invalid-timeout-{Guid.NewGuid():N}");
        var client = new IpcTransportClient();
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                client.SendAsync(endpoint, IpcTransportTestHarness.CreateSingleRequest(), timeout).AsTask(),
                "Invalid timeout send result",
                WaitTimeout);
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

        await IpcTransportTestHarness.WithUnixResponseServerAsync(
            request => invalidField switch
            {
                "protocol" => IpcTransportTestHarness.CreateResponse(request.RequestId, "{}", protocolVersion: IpcProtocol.CurrentVersion + 1),
                "requestId" => IpcTransportTestHarness.CreateResponse("other-request", "{}"),
                "status" => IpcTransportTestHarness.CreateResponse(request.RequestId, "{}", status: "unknown"),
                "errors" => new IpcResponse(
                    IpcProtocol.CurrentVersion,
                    request.RequestId,
                    IpcProtocol.StatusOk,
                    IpcTransportTestHarness.Json("{}"),
                    null!),
                _ => throw new InvalidOperationException("Unsupported invalid field."),
            },
            async (endpoint, request) =>
            {
                var client = new IpcTransportClient();
                var exceptionTask = Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await client.SendAsync(endpoint, request, DefaultTimeout).AsTask();
                });

                await TestAwaiter.WaitAsync(exceptionTask, "Invalid IPC response envelope rejection", WaitTimeout);
            },
            WaitTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenServerReturnsProgressThenTerminal_ForwardsProgressAndReturnsResponse ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await IpcTransportTestHarness.WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcStreamFrameKinds.Progress,
                        "test.progress",
                        IpcTransportTestHarness.Json("""{"progress":true}"""),
                        Response: null),
                    cancellationToken);
                await IpcTransportTestHarness.WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcStreamFrameKinds.Terminal,
                        Event: null,
                        IpcTransportTestHarness.Json("{}"),
                        IpcTransportTestHarness.CreateResponse(request.RequestId, """{"done":true}""")),
                    cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = new IpcTransportClient();
                var progressFrames = new List<IpcStreamFrame>();
                var responseTask = client.SendStreamingAsync(
                        endpoint,
                        request,
                        DefaultTimeout,
                        (frame, _) =>
                        {
                            progressFrames.Add(frame);
                            return ValueTask.CompletedTask;
                        })
                    .AsTask();

                var response = await TestAwaiter.WaitAsync(responseTask, "IPC streaming response", WaitTimeout);

                var progressFrame = Assert.Single(progressFrames);
                Assert.Equal("test.progress", progressFrame.Event);
                Assert.True(progressFrame.Payload.GetProperty("progress").GetBoolean());
                Assert.Equal("request-1", response.RequestId);
                Assert.True(response.Payload.GetProperty("done").GetBoolean());
            },
            WaitTimeout);
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
        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await IpcTransportTestHarness.WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcStreamFrameKinds.Progress,
                        "test.progress",
                        IpcTransportTestHarness.Json("{}"),
                        Response: null),
                    cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = new IpcTransportClient();
                var exceptionTask = Assert.ThrowsAsync<IpcProgressFrameHandlerException>(async () =>
                {
                    await client.SendStreamingAsync(
                            endpoint,
                            request,
                            DefaultTimeout,
                            (_, _) => throw handlerException)
                        .AsTask();
                });

                var exception = await TestAwaiter.WaitAsync(exceptionTask, "IPC streaming progress callback failure", WaitTimeout);
                Assert.Same(handlerException, exception.InnerException);
            },
            WaitTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenProgressRequestIdMismatches_ThrowsInvalidDataException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (_, stream, cancellationToken) =>
            {
                await IpcTransportTestHarness.WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        "other-request",
                        IpcStreamFrameKinds.Progress,
                        "test.progress",
                        IpcTransportTestHarness.Json("{}"),
                        Response: null),
                    cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = new IpcTransportClient();
                var exceptionTask = Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await client.SendStreamingAsync(
                            endpoint,
                            request,
                            DefaultTimeout,
                            (_, _) => ValueTask.CompletedTask)
                        .AsTask();
                });

                await TestAwaiter.WaitAsync(exceptionTask, "IPC streaming requestId mismatch rejection", WaitTimeout);
            },
            WaitTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenFrameKindIsUnsupported_ThrowsInvalidDataException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await IpcTransportTestHarness.WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        "unsupported",
                        "test.progress",
                        IpcTransportTestHarness.Json("{}"),
                        Response: null),
                    cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = new IpcTransportClient();
                var exceptionTask = Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await client.SendStreamingAsync(
                            endpoint,
                            request,
                            DefaultTimeout,
                            (_, _) => ValueTask.CompletedTask)
                        .AsTask();
                });

                await TestAwaiter.WaitAsync(exceptionTask, "IPC streaming unsupported frame kind rejection", WaitTimeout);
            },
            WaitTimeout);
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
            IpcTransportTestHarness.Json("{}"),
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
            IpcTransportTestHarness.Json("{}"),
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
            IpcTransportTestHarness.Json("{}"),
            IpcTransportTestHarness.CreateResponse(request.RequestId, "{}")));
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
            IpcTransportTestHarness.Json("{}"),
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
            IpcTransportTestHarness.Json("{}"),
            IpcTransportTestHarness.CreateResponse(request.RequestId, "{}")));
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
            IpcTransportTestHarness.Json("{}"),
            IpcTransportTestHarness.CreateResponse("other-request", "{}")));
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
            IpcTransportTestHarness.Json("{}"),
            IpcTransportTestHarness.CreateResponse(request.RequestId, "{}", protocolVersion: IpcProtocol.CurrentVersion + 1)));
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
            IpcTransportTestHarness.Json("{}"),
            IpcTransportTestHarness.CreateResponse(request.RequestId, "{}", status: "unknown")));
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
            IpcTransportTestHarness.Json("{}"),
            new IpcResponse(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcProtocol.StatusOk,
                IpcTransportTestHarness.Json("{}"),
                null!)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingWithUnboundedResponseWaitAsync_WhenServerReturnsProgressThenDelayedTerminal_ForwardsProgressAndReturnsResponse ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await IpcTransportTestHarness.WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcStreamFrameKinds.Progress,
                        "test.progress",
                        IpcTransportTestHarness.Json("""{"progress":true}"""),
                        Response: null),
                    cancellationToken);
                await Task.Delay(DelayedTerminalFrameWait, cancellationToken);
                await IpcTransportTestHarness.WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcStreamFrameKinds.Terminal,
                        Event: null,
                        IpcTransportTestHarness.Json("{}"),
                        IpcTransportTestHarness.CreateResponse(request.RequestId, """{"done":true}""")),
                    cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = new IpcTransportClient();
                var progressFrames = new List<IpcStreamFrame>();
                var responseTask = client.SendStreamingWithUnboundedResponseWaitAsync(
                        endpoint,
                        request,
                        DefaultTimeout,
                        (frame, _) =>
                        {
                            progressFrames.Add(frame);
                            return ValueTask.CompletedTask;
                        })
                    .AsTask();

                var response = await TestAwaiter.WaitAsync(responseTask, "Unbounded IPC streaming response", WaitTimeout);

                var progressFrame = Assert.Single(progressFrames);
                Assert.Equal("test.progress", progressFrame.Event);
                Assert.True(progressFrame.Payload.GetProperty("progress").GetBoolean());
                Assert.Equal("request-1", response.RequestId);
                Assert.True(response.Payload.GetProperty("done").GetBoolean());
            },
            WaitTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingWithUnboundedResponseWaitAsync_WhenProgressCallbackThrows_ThrowsProgressFrameHandlerException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var handlerException = new InvalidOperationException("callback failed");
        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await IpcTransportTestHarness.WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcStreamFrameKinds.Progress,
                        "test.progress",
                        IpcTransportTestHarness.Json("{}"),
                        Response: null),
                    cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = new IpcTransportClient();
                var exceptionTask = Assert.ThrowsAsync<IpcProgressFrameHandlerException>(async () =>
                {
                    await client.SendStreamingWithUnboundedResponseWaitAsync(
                            endpoint,
                            request,
                            DefaultTimeout,
                            (_, _) => throw handlerException)
                        .AsTask();
                });

                var exception = await TestAwaiter.WaitAsync(exceptionTask, "Unbounded IPC streaming progress callback failure", WaitTimeout);
                Assert.Same(handlerException, exception.InnerException);
            },
            WaitTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingWithUnboundedResponseWaitAsync_WhenTerminalResponseProtocolVersionMismatches_ThrowsInvalidDataException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await IpcTransportTestHarness.WriteStreamFrameAsync(
                    stream,
                    new IpcStreamFrame(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcStreamFrameKinds.Terminal,
                        Event: null,
                        IpcTransportTestHarness.Json("{}"),
                        IpcTransportTestHarness.CreateResponse(request.RequestId, "{}", protocolVersion: IpcProtocol.CurrentVersion + 1)),
                    cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = new IpcTransportClient();
                var exceptionTask = Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await client.SendStreamingWithUnboundedResponseWaitAsync(
                            endpoint,
                            request,
                            DefaultTimeout,
                            (_, _) => ValueTask.CompletedTask)
                        .AsTask();
                });

                await TestAwaiter.WaitAsync(exceptionTask, "Unbounded IPC streaming terminal validation failure", WaitTimeout);
            },
            WaitTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WithStreamResponseMode_ThrowsInvalidOperationException ()
    {
        var client = new IpcTransportClient();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.SendAsync(
                    new IpcEndpoint(IpcTransportKind.NamedPipe, $"ucli-stream-mode-{Guid.NewGuid():N}"),
                    IpcTransportTestHarness.CreateStreamingRequest(),
                    DefaultTimeout)
                .AsTask();
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WithSingleResponseMode_ThrowsInvalidOperationException ()
    {
        var client = new IpcTransportClient();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.SendStreamingAsync(
                    new IpcEndpoint(IpcTransportKind.NamedPipe, $"ucli-single-mode-{Guid.NewGuid():N}"),
                    IpcTransportTestHarness.CreateSingleRequest(),
                    DefaultTimeout,
                    (_, _) => ValueTask.CompletedTask)
                .AsTask();
        });
    }

    private static async Task AssertStreamingFrameRejectedAsync (Func<IpcRequest, IpcStreamFrame> createFrame)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await IpcTransportTestHarness.WriteStreamFrameAsync(stream, createFrame(request), cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = new IpcTransportClient();
                var exceptionTask = Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await client.SendStreamingAsync(
                            endpoint,
                            request,
                            DefaultTimeout,
                            (_, _) => ValueTask.CompletedTask)
                        .AsTask();
                });

                await TestAwaiter.WaitAsync(exceptionTask, "IPC streaming frame rejection", WaitTimeout);
            },
            WaitTimeout);
    }
}
