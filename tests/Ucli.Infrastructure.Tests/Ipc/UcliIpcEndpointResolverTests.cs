using System.Runtime.InteropServices;
using System.Text;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc;

public sealed class UcliIpcEndpointResolverTests
{
    private const string FingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly ProjectFingerprint Fingerprint = new(FingerprintText);

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDaemonEndpoint_WithNullStorageRoot_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => UcliIpcEndpointResolver.ResolveDaemonEndpoint(null!, Fingerprint));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDaemonEndpoint_WithNullProjectFingerprint_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            AbsolutePath.Parse(Path.GetFullPath(".")),
            null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDaemonEndpoint_WithValidInputs_ReturnsPlatformSpecificEndpoint ()
    {
        var storageRoot = AbsolutePath.Parse(Path.GetFullPath(Path.Combine(".", "sandbox", "Unity")));

        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, Fingerprint);
        var contract = endpoint.Contract;
        var unixSocketPath = UcliIpcEndpointResolver.ResolveDaemonUnixSocketPathOrNull(storageRoot, Fingerprint);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Null(unixSocketPath);
            Assert.Equal(IpcTransportKind.NamedPipe, contract.TransportKind);
            Assert.Equal("ucli-daemon-" + FingerprintText, contract.Address);
            Assert.Null(endpoint.UnixSocketPath);
            return;
        }

        var preferredPath = Path.Combine(
            UcliStoragePathResolver.ResolveProjectDirectory(storageRoot, Fingerprint).Value,
            "ipc.sock");

        Assert.NotNull(unixSocketPath);
        Assert.Equal(IpcTransportKind.UnixDomainSocket, contract.TransportKind);
        Assert.Equal(unixSocketPath, endpoint.UnixSocketPath);
        Assert.Equal(unixSocketPath.Value, contract.Address);
        Assert.True(Encoding.UTF8.GetByteCount(contract.Address) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);

        if (Encoding.UTF8.GetByteCount(preferredPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            Assert.Equal(preferredPath, contract.Address);
            return;
        }

        AssertFallbackPath(contract.Address, "ucli-d-");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDaemonEndpoint_WithLongUnixSocketCandidate_ReturnsShortDeterministicFallbackPath ()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var storageRoot = AbsolutePath.Parse(Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "ucli-tests",
            new string('a', 140))));

        var endpoint1 = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, Fingerprint);
        var endpoint2 = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, Fingerprint);
        var unixSocketPath1 = UcliIpcEndpointResolver.ResolveDaemonUnixSocketPathOrNull(storageRoot, Fingerprint);
        var unixSocketPath2 = UcliIpcEndpointResolver.ResolveDaemonUnixSocketPathOrNull(storageRoot, Fingerprint);

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint1.Contract.TransportKind);
        Assert.NotNull(unixSocketPath1);
        Assert.Equal(unixSocketPath1, unixSocketPath2);
        Assert.Equal(unixSocketPath1, endpoint1.UnixSocketPath);
        Assert.Equal(unixSocketPath1.Value, endpoint1.Contract.Address);
        Assert.Equal(endpoint1.Contract.Address, endpoint2.Contract.Address);
        Assert.True(Encoding.UTF8.GetByteCount(endpoint1.Contract.Address) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);
        AssertFallbackPath(endpoint1.Contract.Address, "ucli-d-");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveGuiSupervisorEndpoint_WithLongUnixSocketCandidate_ReturnsHostSpecificDeterministicFallbackPath ()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var storageRoot = AbsolutePath.Parse(Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "ucli-tests",
            new string('a', 140))));

        var endpoint1 = UcliIpcEndpointResolver.ResolveGuiSupervisorEndpoint(storageRoot, Fingerprint);
        var endpoint2 = UcliIpcEndpointResolver.ResolveGuiSupervisorEndpoint(storageRoot, Fingerprint);

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint1.Contract.TransportKind);
        Assert.Equal(endpoint1.Contract.Address, endpoint2.Contract.Address);
        Assert.True(Encoding.UTF8.GetByteCount(endpoint1.Contract.Address) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);
        AssertFallbackPath(endpoint1.Contract.Address, "ucli-g-");
    }

    private static void AssertFallbackPath (
        string address,
        string directoryPrefix)
    {
        Assert.Equal("ipc.sock", Path.GetFileName(address));

        var directoryPath = Path.GetDirectoryName(address);
        Assert.False(string.IsNullOrWhiteSpace(directoryPath));
        var directoryName = Path.GetFileName(directoryPath);
        Assert.StartsWith(directoryPrefix, directoryName, StringComparison.Ordinal);
        Assert.Equal(
            32,
            directoryName!.Length - directoryPrefix.Length);
        Assert.Equal(
            Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetDirectoryName(directoryPath!)!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}
