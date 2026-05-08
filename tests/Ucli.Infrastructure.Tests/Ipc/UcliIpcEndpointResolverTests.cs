using System.Runtime.InteropServices;
using System.Text;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc;

public sealed class UcliIpcEndpointResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDaemonEndpoint_WithEmptyStorageRoot_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() => UcliIpcEndpointResolver.ResolveDaemonEndpoint("", "fingerprint"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDaemonEndpoint_WithEmptyProjectFingerprint_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() => UcliIpcEndpointResolver.ResolveDaemonEndpoint(".", " "));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDaemonEndpoint_WithValidInputs_ReturnsPlatformSpecificEndpoint ()
    {
        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));

        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, "abc123");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
    public void ResolveDaemonEndpoint_WithLongUnixSocketCandidate_ReturnsShortDeterministicFallbackPath ()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var storageRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "ucli-tests",
            new string('a', 140)));

        var endpoint1 = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, "abc123");
        var endpoint2 = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, "abc123");

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
