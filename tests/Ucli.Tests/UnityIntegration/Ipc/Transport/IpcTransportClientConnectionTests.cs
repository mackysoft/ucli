using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientConnectionTests
{
    [Fact]
    [Trait("Size", "Medium")]
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
                client.SendAsync(endpoint, IpcTransportTestHarness.CreateSingleRequest(), IpcTransportClientTestSupport.DefaultTimeout).AsTask(),
                "Missing named pipe send result",
                IpcTransportClientTestSupport.WaitTimeout);
        });
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
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
            await TestAwaiter.WaitAsync(
                sendTask,
                "Canceled IPC send",
                IpcTransportClientTestSupport.WaitTimeout);
        });
    }
}
