using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientRequestValidationTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendAsync_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException (int timeoutMilliseconds)
    {
        var endpoint = new IpcEndpoint(
            IpcTransportKind.NamedPipe,
            "ucli-invalid-timeout");
        var client = new IpcTransportClient();
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await client.SendAsync(endpoint, IpcTransportTestHarness.CreateSingleRequest(), timeout);
        });
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
                    IpcTransportClientTestSupport.DefaultTimeout)
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
                    IpcTransportClientTestSupport.DefaultTimeout,
                    (_, _) => ValueTask.CompletedTask)
                .AsTask();
        });
    }
}
