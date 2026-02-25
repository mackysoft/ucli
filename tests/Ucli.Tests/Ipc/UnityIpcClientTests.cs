using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Ipc;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenNamedPipeServerIsMissing_ThrowsTimeoutException ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var endpointResolver = new FixedEndpointResolver(
            new IpcEndpoint(
                IpcTransportKind.NamedPipe,
                $"ucli-missing-{Guid.NewGuid():N}"));
        var client = new UnityIpcClient(endpointResolver);
        var request = new IpcRequest(
            IpcProtocol.CurrentVersion,
            "request-1",
            "token",
            IpcMethodNames.Ping,
            JsonDocument.Parse("{}").RootElement.Clone());

        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await client.SendAsync("project-root", "fingerprint", request).AsTask();
        });
        Assert.Contains("named pipe", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        var client = new UnityIpcClient(endpointResolver);
        var request = new IpcRequest(
            IpcProtocol.CurrentVersion,
            "request-1",
            "token",
            IpcMethodNames.Ping,
            JsonDocument.Parse("{}").RootElement.Clone());
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await client.SendAsync(
                    "project-root",
                    "fingerprint",
                    request,
                    cancellationTokenSource.Token)
                .AsTask();
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
            string projectRoot,
            string projectFingerprint)
        {
            return endpoint;
        }
    }
}
