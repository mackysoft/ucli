using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientStreamingTests
{
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
                var client = new IpcTransportClient();
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

                IpcTransportClientTestSupport.AssertProgressThenTerminalResult(progressFrames, response);
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
                var client = new IpcTransportClient();
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
                    var client = new IpcTransportClient();
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
                    Assert.Equal(IpcStreamFrameKinds.Progress, progressFrame.Kind);
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
                    var client = new IpcTransportClient();
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
                    Assert.Equal(IpcStreamFrameKinds.Progress, progressFrame.Kind);
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
}
