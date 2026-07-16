using System.Text;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorEndpointResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void WorktreeIdentity_UsesNormalizedStorageRootForFixedPurposeSpecificSegments ()
    {
        var storageRoot = Path.Combine(".", "sandbox", "Supervisor", "..");

        var identity = SupervisorWorktreeIdentity.Create(storageRoot);

        Assert.Equal(Path.GetFullPath(storageRoot), identity.NormalizedStorageRoot);
        Assert.Equal(16, identity.LaunchServiceNameSuffix.Length);
        Assert.Equal(24, identity.NamedPipeAddressSegment.Length);
        Assert.StartsWith(identity.LaunchServiceNameSuffix, identity.NamedPipeAddressSegment, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SupervisorUnixSocketCleanupTarget_WithRelativePath_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(
            () => new SupervisorUnixSocketCleanupTarget("relative-supervisor.sock"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateNamedPipeGenerationAddress_WithDifferentSessionToken_ReturnsDistinctStableNames ()
    {
        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Supervisor"));
        var firstSessionToken = IpcSessionTokenTestFactory.CreateFromDiscriminator(1);
        var secondSessionToken = IpcSessionTokenTestFactory.CreateFromDiscriminator(2);

        var first = SupervisorEndpointResolver.CreateNamedPipeGenerationAddress(storageRoot, firstSessionToken);
        var firstAgain = SupervisorEndpointResolver.CreateNamedPipeGenerationAddress(storageRoot, firstSessionToken);
        var second = SupervisorEndpointResolver.CreateNamedPipeGenerationAddress(storageRoot, secondSessionToken);

        Assert.Equal(first, firstAgain);
        Assert.NotEqual(first, second);
        var worktreeIdentity = SupervisorWorktreeIdentity.Create(storageRoot);
        Assert.StartsWith(
            UcliIpcEndpointNames.SupervisorAddressPrefix + worktreeIdentity.NamedPipeAddressSegment + "-",
            first,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveUnixSocketCleanupTargetOrNull_ReturnsOnlyARealFilesystemCleanupTarget ()
    {
        var resolver = new SupervisorEndpointResolver();
        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Supervisor"));

        var cleanupTarget = resolver.ResolveUnixSocketCleanupTargetOrNull(storageRoot);

        if (OperatingSystem.IsWindows())
        {
            Assert.Null(cleanupTarget);
            return;
        }

        Assert.NotNull(cleanupTarget);
        Assert.True(Path.IsPathFullyQualified(cleanupTarget.SocketPath));
        Assert.True(Encoding.UTF8.GetByteCount(cleanupTarget.SocketPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveRuntimeEndpoint_WithEmptyStorageRoot_ThrowsArgumentException ()
    {
        var resolver = new SupervisorEndpointResolver();
        var sessionToken = IpcSessionTokenTestFactory.CreateFromDiscriminator(1);

        Assert.Throws<ArgumentException>(() => resolver.ResolveRuntimeEndpoint("", sessionToken));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveRuntimeEndpoint_WithValidInputs_ReturnsPlatformSpecificEndpoint ()
    {
        var resolver = new SupervisorEndpointResolver();
        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Supervisor"));
        var sessionToken = IpcSessionTokenTestFactory.CreateFromDiscriminator(1);

        var endpoint = resolver.ResolveRuntimeEndpoint(storageRoot, sessionToken);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(IpcTransportKind.NamedPipe, endpoint.TransportKind);
            Assert.Equal(
                SupervisorEndpointResolver.CreateNamedPipeGenerationAddress(
                    storageRoot,
                    sessionToken),
                endpoint.Address);
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

        AssertFallbackPath(endpoint.Address, "ucli-s-");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveRuntimeEndpoint_WithLongUnixSocketCandidate_ReturnsShortDeterministicFallbackPath ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var resolver = new SupervisorEndpointResolver();
        var sessionToken = IpcSessionTokenTestFactory.CreateFromDiscriminator(1);
        var storageRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "ucli-tests",
            new string('a', 140)));

        var endpoint1 = resolver.ResolveRuntimeEndpoint(storageRoot, sessionToken);
        var endpoint2 = resolver.ResolveRuntimeEndpoint(storageRoot, sessionToken);

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint1.TransportKind);
        Assert.Equal(endpoint1.Address, endpoint2.Address);
        Assert.True(Encoding.UTF8.GetByteCount(endpoint1.Address) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);
        AssertFallbackPath(endpoint1.Address, "ucli-s-");
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
