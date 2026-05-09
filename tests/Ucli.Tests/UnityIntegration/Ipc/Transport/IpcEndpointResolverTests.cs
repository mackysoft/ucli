using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcEndpointResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsSharedDaemonEndpoint ()
    {
        var resolver = new IpcEndpointResolver();
        var storageRoot = OperatingSystem.IsWindows()
            ? Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"))
            : "/tmp/ucli-ipc-endpoint-wrapper";

        var endpoint = resolver.Resolve(storageRoot, "abc123");

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(IpcTransportKind.NamedPipe, endpoint.TransportKind);
            Assert.Equal("ucli-daemon-abc123", endpoint.Address);
            return;
        }

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.TransportKind);
        Assert.Equal(
            Path.Combine(storageRoot, ".ucli", "local", "fingerprints", "abc123", "ipc.sock"),
            endpoint.Address);
    }
}
