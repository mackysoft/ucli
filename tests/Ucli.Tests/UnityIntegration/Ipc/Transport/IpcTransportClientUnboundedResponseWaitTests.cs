using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientUnboundedResponseWaitTests
{
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

                var exception = await exceptionTask.WaitAsync(IpcTransportClientTestSupport.WaitTimeout);
                Assert.Same(handlerException, exception.InnerException);
            },
            IpcTransportClientTestSupport.WaitTimeout);
    }
}
