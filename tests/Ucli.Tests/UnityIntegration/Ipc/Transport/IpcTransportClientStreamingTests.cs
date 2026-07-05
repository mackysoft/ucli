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
}
