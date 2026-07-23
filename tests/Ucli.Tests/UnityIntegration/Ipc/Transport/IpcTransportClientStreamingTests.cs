using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientStreamingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Medium")]
    public async Task SendStreamingResponseAsync_WhenServerClosesAfterReadingRequest_ThrowsResponseReadInterrupted (
        bool useUnboundedResponseWait)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            static (_, _, _) => Task.CompletedTask,
            async (endpoint, request) =>
            {
                var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);
                var exceptionTask = Assert.ThrowsAsync<IpcResponseReadInterruptedException>(async () =>
                {
                    if (useUnboundedResponseWait)
                    {
                        await client.SendStreamingWithUnboundedResponseWaitAsync(
                                endpoint,
                                request,
                                IpcTransportClientTestSupport.DefaultTimeout,
                                (_, _) => ValueTask.CompletedTask)
                            .AsTask();
                    }
                    else
                    {
                        await client.SendStreamingAsync(
                                endpoint,
                                request,
                                IpcTransportClientTestSupport.DefaultTimeout,
                                (_, _) => ValueTask.CompletedTask)
                            .AsTask();
                    }
                });

                var exception = await TestAwaiter.WaitAsync(
                    exceptionTask,
                    "Interrupted IPC streaming response read",
                    IpcTransportClientTestSupport.WaitTimeout);
                Assert.IsType<EndOfStreamException>(exception.InnerException);
            },
            IpcTransportClientTestSupport.WaitTimeout);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendStreamingAsync_WhenServerReturnsProgressThenTerminal_ForwardsProgressAndReturnsResponse ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await IpcTransportClientTestSupport.WriteProgressThenTerminalAsync(
                    request,
                    stream,
                    cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);
                var progressFrames = new List<IpcStreamFrame>();
                var responseTask = client.SendStreamingAsync(
                        endpoint,
                        request,
                        IpcTransportClientTestSupport.DefaultTimeout,
                        (frame, _) =>
                        {
                            progressFrames.Add(frame);
                            return ValueTask.CompletedTask;
                        })
                    .AsTask();

                var response = await TestAwaiter.WaitAsync(
                    responseTask,
                    "IPC streaming response",
                    IpcTransportClientTestSupport.WaitTimeout);

                IpcTransportClientTestSupport.AssertProgressThenTerminalResult(
                    progressFrames,
                    response,
                    request.RequestId);
            },
            IpcTransportClientTestSupport.WaitTimeout);
    }

    [Fact]
    [Trait("Size", "Medium")]
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
                    IpcTransportClientTestSupport.CreateProgressFrame(request),
                    cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);
                var exceptionTask = Assert.ThrowsAsync<IpcProgressFrameHandlerException>(async () =>
                {
                    await client.SendStreamingAsync(
                            endpoint,
                            request,
                            IpcTransportClientTestSupport.DefaultTimeout,
                            (_, _) => throw handlerException)
                        .AsTask();
                });

                var exception = await TestAwaiter.WaitAsync(
                    exceptionTask,
                    "IPC streaming progress callback failure",
                    IpcTransportClientTestSupport.WaitTimeout);
                Assert.Same(handlerException, exception.InnerException);
            },
            IpcTransportClientTestSupport.WaitTimeout);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendStreamingAsync_WhenProgressCallbackDoesNotCompleteAndBlocksCancellation_ThrowsTimeoutException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var callbackCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackStartedSource = new TaskCompletionSource<IpcStreamFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackExitedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationCallbackStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationCallbackCompletedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCancellationCallbackSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<TimeoutException>? timeoutExceptionTask = null;

        try
        {
            await IpcTransportTestHarness.WithUnixStreamingServerAsync(
                async (request, stream, cancellationToken) =>
                {
                    await IpcTransportTestHarness.WriteStreamFrameAsync(
                        stream,
                        IpcTransportClientTestSupport.CreateProgressFrame(request),
                        cancellationToken);
                },
                async (endpoint, request) =>
                {
                    var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);
                    var currentTimeoutExceptionTask = Assert.ThrowsAsync<TimeoutException>(async () =>
                    {
                        await client.SendStreamingAsync(
                                endpoint,
                                request,
                                IpcTransportClientTestSupport.DefaultTimeout,
                                async (frame, callbackCancellationToken) =>
                                {
                                    callbackStartedSource.TrySetResult(frame);
                                    using var cancellationRegistration = callbackCancellationToken.Register(() =>
                                    {
                                        cancellationCallbackStartedSource.TrySetResult();
                                        try
                                        {
                                            releaseCancellationCallbackSource.Task.GetAwaiter().GetResult();
                                        }
                                        finally
                                        {
                                            cancellationCallbackCompletedSource.TrySetResult();
                                        }
                                    });

                                    try
                                    {
                                        await callbackCompletionSource.Task.ConfigureAwait(false);
                                    }
                                    finally
                                    {
                                        callbackExitedSource.TrySetResult();
                                    }
                                })
                            .AsTask();
                    });
                    timeoutExceptionTask = currentTimeoutExceptionTask;

                    var progressFrame = await TestAwaiter.WaitAsync(
                        callbackStartedSource.Task,
                        "IPC streaming progress callback start",
                        IpcTransportClientTestSupport.WaitTimeout);
                    var exception = await TestAwaiter.WaitAsync(
                        currentTimeoutExceptionTask,
                        "Bounded IPC streaming callback timeout",
                        IpcTransportClientTestSupport.WaitTimeout);
                    await TestAwaiter.WaitAsync(
                        cancellationCallbackStartedSource.Task,
                        "IPC streaming callback cancellation request",
                        IpcTransportClientTestSupport.WaitTimeout);

                    Assert.Contains("IPC streaming request timed out after", exception.Message, StringComparison.Ordinal);
                    Assert.Equal(IpcStreamFrameKind.Progress, progressFrame.Kind);
                    Assert.Equal(request.RequestId, progressFrame.RequestId);
                    Assert.Equal("test.progress", progressFrame.Event);
                    Assert.True(progressFrame.Payload.GetProperty("progress").GetBoolean());
                },
                IpcTransportClientTestSupport.WaitTimeout);
        }
        finally
        {
            if (timeoutExceptionTask is not null)
            {
                _ = timeoutExceptionTask.ContinueWith(
                    static task => _ = task.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }

            releaseCancellationCallbackSource.TrySetResult();
            callbackCompletionSource.TrySetException(new InvalidOperationException("late progress callback failure"));
            if (cancellationCallbackStartedSource.Task.IsCompleted)
            {
                await TestAwaiter.WaitAsync(
                    cancellationCallbackCompletedSource.Task,
                    "IPC streaming callback cancellation completion",
                    IpcTransportClientTestSupport.WaitTimeout);
            }

            if (callbackStartedSource.Task.IsCompleted)
            {
                await TestAwaiter.WaitAsync(
                    callbackExitedSource.Task,
                    "IPC streaming callback completion",
                    IpcTransportClientTestSupport.WaitTimeout);
            }
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendStreamingAsync_WhenProgressCallbackBlocksBeforeReturningValueTask_ThrowsTimeoutException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var callbackStartedSource = new TaskCompletionSource<IpcStreamFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackExitedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowCallbackToReturn = new ManualResetEventSlim();
        Task<TimeoutException>? timeoutExceptionTask = null;

        try
        {
            await IpcTransportTestHarness.WithUnixStreamingServerAsync(
                async (request, stream, cancellationToken) =>
                {
                    await IpcTransportTestHarness.WriteStreamFrameAsync(
                        stream,
                        IpcTransportClientTestSupport.CreateProgressFrame(request),
                        cancellationToken);
                },
                async (endpoint, request) =>
                {
                    var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);
                    var currentTimeoutExceptionTask = Assert.ThrowsAsync<TimeoutException>(async () =>
                    {
                        await client.SendStreamingAsync(
                                endpoint,
                                request,
                                IpcTransportClientTestSupport.DefaultTimeout,
                                (frame, _) =>
                                {
                                    callbackStartedSource.TrySetResult(frame);
                                    try
                                    {
                                        allowCallbackToReturn.Wait();
                                        return ValueTask.CompletedTask;
                                    }
                                    finally
                                    {
                                        callbackExitedSource.TrySetResult();
                                    }
                                })
                            .AsTask();
                    });
                    timeoutExceptionTask = currentTimeoutExceptionTask;

                    var progressFrame = await TestAwaiter.WaitAsync(
                        callbackStartedSource.Task,
                        "synchronously blocking IPC progress callback start",
                        IpcTransportClientTestSupport.WaitTimeout);
                    var exception = await TestAwaiter.WaitAsync(
                        currentTimeoutExceptionTask,
                        "bounded IPC streaming synchronous callback timeout",
                        IpcTransportClientTestSupport.WaitTimeout);

                    Assert.Contains("IPC streaming request timed out after", exception.Message, StringComparison.Ordinal);
                    Assert.Equal(IpcStreamFrameKind.Progress, progressFrame.Kind);
                    Assert.Equal(request.RequestId, progressFrame.RequestId);
                },
                IpcTransportClientTestSupport.WaitTimeout);
        }
        finally
        {
            if (timeoutExceptionTask is not null)
            {
                _ = timeoutExceptionTask.ContinueWith(
                    static task => _ = task.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }

            allowCallbackToReturn.Set();
            if (callbackStartedSource.Task.IsCompleted)
            {
                await TestAwaiter.WaitAsync(
                    callbackExitedSource.Task,
                    "synchronously blocking IPC progress callback completion",
                    IpcTransportClientTestSupport.WaitTimeout);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Small")]
    public async Task SendStreamingAsync_WhenTimedOutProgressCallbackHasNotConverged_RejectsAnotherStreamUntilItConverges (
        bool blockCancellationHandler)
    {
        var firstRequest = IpcTransportTestHarness.CreateStreamingRequest();
        var rejectedRequest = IpcTransportTestHarness.CreateStreamingRequest();
        var singleRequest = IpcTransportTestHarness.CreateSingleRequest();
        var resumedRequest = IpcTransportTestHarness.CreateStreamingRequest();
        var firstStream = new DuplexMemoryStream(await CreateModelBytesAsync(
            IpcTransportClientTestSupport.CreateProgressFrame(firstRequest)));
        var singleStream = new DuplexMemoryStream(await CreateModelBytesAsync(
            IpcTransportTestHarness.CreateResponse(singleRequest.RequestId, """{"single":true}""")));
        var resumedStream = new DuplexMemoryStream(await CreateModelBytesAsync(
            IpcTransportClientTestSupport.CreateTerminalFrame(resumedRequest)));
        var connector = new SequencedConnector(firstStream, singleStream, resumedStream);
        var timeProvider = new ManualTimeProvider();
        var client = new IpcTransportClient(connector, timeProvider);
        var endpoint = IpcTransportEndpoint.FromNamedPipeAddress("stream-admission-test");
        var callbackStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackExitedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationHandlerStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationHandlerReleaseSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstSendTask = client.SendStreamingAsync(
                endpoint,
                firstRequest,
                IpcTransportClientTestSupport.DefaultTimeout,
                async (_, cancellationToken) =>
                {
                    using var cancellationRegistration = blockCancellationHandler
                        ? cancellationToken.Register(() =>
                        {
                            cancellationHandlerStartedSource.TrySetResult();
                            cancellationHandlerReleaseSource.Task.GetAwaiter().GetResult();
                        })
                        : default;
                    callbackStartedSource.TrySetResult();
                    try
                    {
                        await callbackCompletionSource.Task.ConfigureAwait(false);
                    }
                    finally
                    {
                        callbackExitedSource.TrySetResult();
                    }
                })
            .AsTask();

        try
        {
            await TestAwaiter.WaitAsync(
                callbackStartedSource.Task,
                "initial IPC streaming callback start",
                IpcTransportClientTestSupport.WaitTimeout);
            await TestAwaiter.WaitAsync(
                timeProvider.WaitForTimerDueWithinAsync(IpcTransportClientTestSupport.DefaultTimeout),
                "initial IPC streaming deadline registration",
                IpcTransportClientTestSupport.WaitTimeout);
            timeProvider.Advance(IpcTransportClientTestSupport.DefaultTimeout);

            await TestAwaiter.WaitAsync(
                Assert.ThrowsAsync<TimeoutException>(async () => await firstSendTask),
                "initial IPC streaming outward timeout",
                IpcTransportClientTestSupport.WaitTimeout);
            if (blockCancellationHandler)
            {
                await TestAwaiter.WaitAsync(
                    cancellationHandlerStartedSource.Task,
                    "initial IPC streaming cancellation handler start",
                    IpcTransportClientTestSupport.WaitTimeout);
            }

            var rejection = await Assert.ThrowsAsync<IpcStreamingOperationInProgressException>(async () =>
            {
                await client.SendStreamingWithUnboundedResponseWaitAsync(
                        endpoint,
                        rejectedRequest,
                        IpcTransportClientTestSupport.DefaultTimeout,
                        static (_, _) => ValueTask.CompletedTask)
                    .AsTask();
            });

            Assert.Contains("previous IPC streaming operation", rejection.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, connector.ConnectionCount);

            var singleResponse = await client.SendAsync(
                endpoint,
                singleRequest,
                IpcTransportClientTestSupport.DefaultTimeout);
            Assert.Equal(singleRequest.RequestId, singleResponse.RequestId);
            Assert.Equal(2, connector.ConnectionCount);

            cancellationHandlerReleaseSource.TrySetResult();
            callbackCompletionSource.TrySetResult();
            await TestAwaiter.WaitAsync(
                callbackExitedSource.Task,
                "initial IPC streaming callback convergence",
                IpcTransportClientTestSupport.WaitTimeout);

            var response = await SendAfterPreviousOperationConvergesAsync(
                client,
                endpoint,
                resumedRequest);

            Assert.Equal(resumedRequest.RequestId, response.RequestId);
            Assert.Equal(3, connector.ConnectionCount);
        }
        finally
        {
            cancellationHandlerReleaseSource.TrySetResult();
            callbackCompletionSource.TrySetResult();
            await firstStream.DisposeAsync();
            await singleStream.DisposeAsync();
            await resumedStream.DisposeAsync();
        }
    }

    private static async Task<IpcResponse> SendAfterPreviousOperationConvergesAsync (
        IpcTransportClient client,
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request)
    {
        using var waitCancellationTokenSource = new CancellationTokenSource(IpcTransportClientTestSupport.WaitTimeout);
        while (true)
        {
            try
            {
                return await client.SendStreamingAsync(
                        endpoint,
                        request,
                        IpcTransportClientTestSupport.DefaultTimeout,
                        static (_, _) => ValueTask.CompletedTask,
                        waitCancellationTokenSource.Token)
                    .ConfigureAwait(false);
            }
            catch (IpcStreamingOperationInProgressException) when (!waitCancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), waitCancellationTokenSource.Token).ConfigureAwait(false);
            }
        }
    }

    private static async Task<byte[]> CreateModelBytesAsync<T> (T model)
    {
        await using var stream = new MemoryStream();
        await IpcFrameCodec.WriteModelAsync(
            stream,
            model,
            IpcJsonSerializerOptions.Default,
            cancellationToken: CancellationToken.None);
        return stream.ToArray();
    }

    private sealed class SequencedConnector : IIpcTransportConnector
    {
        private readonly Queue<Stream> streams;

        private int connectionCount;

        public SequencedConnector (params Stream[] streams)
        {
            this.streams = new Queue<Stream>(streams);
        }

        public int ConnectionCount => Volatile.Read(ref connectionCount);

        public ValueTask<Stream> ConnectAsync (
            IpcTransportEndpoint endpoint,
            CancellationToken cancellationToken)
        {
            _ = endpoint;
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref connectionCount);
            return ValueTask.FromResult(streams.Dequeue());
        }
    }
}
