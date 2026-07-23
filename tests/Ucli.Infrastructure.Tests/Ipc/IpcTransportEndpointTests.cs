using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc;

public sealed class IpcTransportEndpointTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FromUnixSocketPath_RetainsGuardedPathWithoutReparsing ()
    {
        var socketPath = AbsolutePath.Parse(Path.Combine(Path.GetTempPath(), "ucli-transport-endpoint.sock"));

        var endpoint = IpcTransportEndpoint.FromUnixSocketPath(socketPath);

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.Contract.TransportKind);
        Assert.Equal(socketPath.Value, endpoint.Contract.Address);
        Assert.Same(socketPath, endpoint.UnixSocketPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromContract_WithUnixSocketAddress_ParsesAndRetainsGuardedPathAtBoundary ()
    {
        var rawAddress = Path.Combine(
            Path.GetTempPath(),
            ".",
            "ucli-transport-endpoint.sock");
        var endpointContract = new IpcEndpoint(IpcTransportKind.UnixDomainSocket, rawAddress);

        var endpoint = IpcTransportEndpoint.FromContract(endpointContract);

        Assert.NotNull(endpoint.UnixSocketPath);
        Assert.Equal(endpoint.UnixSocketPath.Value, endpoint.Contract.Address);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromNamedPipeAddress_DoesNotInterpretLogicalAddressAsPath ()
    {
        var endpoint = IpcTransportEndpoint.FromNamedPipeAddress("ucli-test-pipe");

        Assert.Equal(IpcTransportKind.NamedPipe, endpoint.Contract.TransportKind);
        Assert.Equal("ucli-test-pipe", endpoint.Contract.Address);
        Assert.Null(endpoint.UnixSocketPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromContract_WithRelativeUnixSocketAddress_ThrowsPathValidationException ()
    {
        var endpointContract = new IpcEndpoint(
            IpcTransportKind.UnixDomainSocket,
            "relative/ucli-transport-endpoint.sock");

        Assert.Throws<PathValidationException>(() => IpcTransportEndpoint.FromContract(endpointContract));
    }
}
