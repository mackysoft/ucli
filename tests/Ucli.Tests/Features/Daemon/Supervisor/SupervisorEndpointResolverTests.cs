using System.Text;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor;
using MackySoft.Ucli.UnityIntegration.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorEndpointResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithEmptyStorageRoot_ThrowsArgumentException ()
    {
        var resolver = new SupervisorEndpointResolver();

        Assert.Throws<ArgumentException>(() => resolver.Resolve(""));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithValidInputs_ReturnsPlatformSpecificEndpoint ()
    {
        var resolver = new SupervisorEndpointResolver();
        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Supervisor"));

        var endpoint = resolver.Resolve(storageRoot);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(IpcTransportKind.NamedPipe, endpoint.TransportKind);
            Assert.StartsWith(UcliIpcEndpointNames.SupervisorAddressPrefix, endpoint.Address, StringComparison.Ordinal);
            return;
        }

        var preferredPath = Path.Combine(storageRoot, ".ucli", "local", "supervisor", "ipc.sock");

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.TransportKind);
        Assert.True(Encoding.UTF8.GetByteCount(endpoint.Address) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);

        if (Encoding.UTF8.GetByteCount(preferredPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            Assert.Equal(preferredPath, endpoint.Address);
            return;
        }

        AssertFallbackPath(endpoint.Address, UcliIpcEndpointNames.SupervisorAddressPrefix);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithLongUnixSocketCandidate_ReturnsShortDeterministicFallbackPath ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var resolver = new SupervisorEndpointResolver();
        var storageRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "ucli-tests",
            new string('a', 140)));

        var endpoint1 = resolver.Resolve(storageRoot);
        var endpoint2 = resolver.Resolve(storageRoot);

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint1.TransportKind);
        Assert.Equal(endpoint1.Address, endpoint2.Address);
        Assert.True(Encoding.UTF8.GetByteCount(endpoint1.Address) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);
        AssertFallbackPath(endpoint1.Address, UcliIpcEndpointNames.SupervisorAddressPrefix);
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