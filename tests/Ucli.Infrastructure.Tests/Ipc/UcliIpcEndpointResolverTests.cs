using System.Runtime.InteropServices;
using System.Text;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc;

public sealed class UcliIpcEndpointResolverTests
{
    private const string FingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly ProjectFingerprint Fingerprint = new(FingerprintText);

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDaemonEndpoint_WithEmptyStorageRoot_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() => UcliIpcEndpointResolver.ResolveDaemonEndpoint("", Fingerprint));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDaemonEndpoint_WithNullProjectFingerprint_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => UcliIpcEndpointResolver.ResolveDaemonEndpoint(".", null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDaemonEndpoint_WithValidInputs_ReturnsPlatformSpecificEndpoint ()
    {
        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));

        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, Fingerprint);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal(IpcTransportKind.NamedPipe, endpoint.TransportKind);
            Assert.Equal("ucli-daemon-" + FingerprintText, endpoint.Address);
            return;
        }

        var preferredPath = Path.Combine(storageRoot, ".ucli", "local", "fingerprints", FingerprintText, "ipc.sock");

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

        var endpoint1 = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, Fingerprint);
        var endpoint2 = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, Fingerprint);

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
