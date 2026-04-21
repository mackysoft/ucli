using System.Text;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcEndpointResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithEmptyStorageRoot_ThrowsArgumentException ()
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
        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));

        var endpoint = resolver.Resolve(storageRoot, "abc123");

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(IpcTransportKind.NamedPipe, endpoint.TransportKind);
            Assert.Equal("ucli-daemon-abc123", endpoint.Address);
            return;
        }

        var preferredPath = Path.Combine(storageRoot, ".ucli", "local", "fingerprints", "abc123", "ipc.sock");

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.TransportKind);
        Assert.True(Encoding.UTF8.GetByteCount(endpoint.Address) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);

        if (Encoding.UTF8.GetByteCount(preferredPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            Assert.Equal(preferredPath, endpoint.Address);
            return;
        }

        AssertFallbackPath(endpoint.Address, UcliIpcEndpointNames.DaemonAddressPrefix);
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
        var storageRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "ucli-tests",
            new string('a', 140)));

        var endpoint1 = resolver.Resolve(storageRoot, "abc123");
        var endpoint2 = resolver.Resolve(storageRoot, "abc123");

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint1.TransportKind);
        Assert.Equal(endpoint1.Address, endpoint2.Address);
        Assert.True(Encoding.UTF8.GetByteCount(endpoint1.Address) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);
        AssertFallbackPath(endpoint1.Address, UcliIpcEndpointNames.DaemonAddressPrefix);
    }

    private static void AssertFallbackPath (
        string address,
        string directoryPrefix)
    {
        Assert.Equal("ipc.sock", Path.GetFileName(address));

        var directoryPath = Path.GetDirectoryName(address);
        Assert.False(string.IsNullOrWhiteSpace(directoryPath));
        Assert.StartsWith(directoryPrefix, Path.GetFileName(directoryPath), StringComparison.Ordinal);
        Assert.Equal(
            Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetDirectoryName(directoryPath!)!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}
