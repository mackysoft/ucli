using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Ipc;

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