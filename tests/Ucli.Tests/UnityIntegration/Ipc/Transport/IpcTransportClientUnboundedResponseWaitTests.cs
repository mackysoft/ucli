using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientUnboundedResponseWaitTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendStreamingWithUnboundedResponseWaitAsync_WhenServerReturnsProgressThenDelayedTerminal_ForwardsProgressAndReturnsResponse ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await IpcTransportClientTestSupport.WriteProgressThenDelayedTerminalAsync(
                    request,
                    stream,
                    cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);
                var progressFrames = new List<IpcStreamFrame>();
                var responseTask = client.SendStreamingWithUnboundedResponseWaitAsync(
                        endpoint,
                        request,
                        IpcTransportClientTestSupport.UnboundedSendTimeout,
                        (frame, _) =>
                        {
                            progressFrames.Add(frame);
                            return ValueTask.CompletedTask;
                        })
                    .AsTask();

                var response = await TestAwaiter.WaitAsync(
                    responseTask,
                    "Unbounded IPC streaming response",
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
                    IpcTransportClientTestSupport.CreateProgressFrame(request),
                    cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);
                var exceptionTask = Assert.ThrowsAsync<IpcProgressFrameHandlerException>(async () =>
                {
                    await client.SendStreamingWithUnboundedResponseWaitAsync(
                            endpoint,
                            request,
                            IpcTransportClientTestSupport.DefaultTimeout,
                            (_, _) => throw handlerException)
                        .AsTask();
                });

                var exception = await TestAwaiter.WaitAsync(
                    exceptionTask,
                    "Unbounded IPC streaming progress callback failure",
                    IpcTransportClientTestSupport.WaitTimeout);
                Assert.Same(handlerException, exception.InnerException);
            },
            IpcTransportClientTestSupport.WaitTimeout);
    }

}
