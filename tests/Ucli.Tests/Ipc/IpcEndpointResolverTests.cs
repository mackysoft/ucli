using System.Text;
using MackySoft.Ucli.Ipc;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcEndpointResolverTests
{
    private const int UnixDomainSocketPathMaxBytes = 103;

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

        var preferredPath = Path.Combine(projectRoot, ".ucli", "local", "abc123", "ipc.sock");

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.TransportKind);
        Assert.True(Encoding.UTF8.GetByteCount(endpoint.Address) <= UnixDomainSocketPathMaxBytes);

        if (Encoding.UTF8.GetByteCount(preferredPath) <= UnixDomainSocketPathMaxBytes)
        {
            Assert.Equal(preferredPath, endpoint.Address);
            return;
        }

        Assert.StartsWith("/tmp/ucli-", endpoint.Address);
        Assert.EndsWith(".sock", endpoint.Address);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithLongUnixSocketCandidate_ReturnsShortDeterministicFallbackPath ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var resolver = new IpcEndpointResolver();
        var projectRoot = Path.GetFullPath(Path.Combine(
            "/tmp",
            "ucli-tests",
            new string('a', 140)));

        var endpoint1 = resolver.Resolve(projectRoot, "abc123");
        var endpoint2 = resolver.Resolve(projectRoot, "abc123");

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint1.TransportKind);
        Assert.Equal(endpoint1.Address, endpoint2.Address);
        Assert.StartsWith("/tmp/ucli-", endpoint1.Address);
        Assert.EndsWith(".sock", endpoint1.Address);
        Assert.True(Encoding.UTF8.GetByteCount(endpoint1.Address) <= UnixDomainSocketPathMaxBytes);
    }
}
