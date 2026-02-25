using MackySoft.Ucli.Ipc;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcEndpointResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithEmptyProjectRoot_ThrowsArgumentException ()
    {
        var resolver = new IpcEndpointResolver();

        Assert.Throws<ArgumentException>(() => resolver.Resolve("", "fingerprint"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithEmptyProjectFingerprint_ThrowsArgumentException ()
    {
        var resolver = new IpcEndpointResolver();

        Assert.Throws<ArgumentException>(() => resolver.Resolve(".", " "));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithValidInputs_ReturnsPlatformSpecificEndpoint ()
    {
        var resolver = new IpcEndpointResolver();
        var projectRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));

        var endpoint = resolver.Resolve(projectRoot, "abc123");

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(IpcTransportKind.NamedPipe, endpoint.TransportKind);
            Assert.Equal("ucli-abc123", endpoint.Address);
            return;
        }

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.TransportKind);
        Assert.Equal(
            Path.Combine(projectRoot, ".ucli", "local", "abc123", "ipc.sock"),
            endpoint.Address);
    }
}
