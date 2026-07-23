using System.Runtime.Versioning;
using System.Text;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorEndpointResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void WorktreeIdentity_UsesGuardedStorageRootForFixedPurposeSpecificSegments ()
    {
        var storageRoot = AbsolutePath.Parse(Path.GetFullPath(Path.Combine(".", "sandbox")));

        var identity = SupervisorWorktreeIdentity.Create(storageRoot);

        Assert.Equal(storageRoot, identity.NormalizedStorageRoot);
        Assert.Equal(16, identity.LaunchServiceNameSuffix.Length);
        Assert.Equal(24, identity.NamedPipeAddressSegment.Length);
        Assert.StartsWith(identity.LaunchServiceNameSuffix, identity.NamedPipeAddressSegment, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("windows")]
    public void WorktreeIdentity_OnWindows_WithStorageRootCaseVariant_ReturnsSameSegments ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var storageRoot = AbsolutePath.Parse(
            Path.GetFullPath(Path.Combine(".", "sandbox", "Supervisor")));
        var storageRootCaseVariant = AbsolutePath.Parse(SwapLetterCase(storageRoot.Value));

        var primary = SupervisorWorktreeIdentity.Create(storageRoot);
        var secondary = SupervisorWorktreeIdentity.Create(storageRootCaseVariant);

        Assert.Equal(storageRoot, storageRootCaseVariant);
        Assert.NotEqual(storageRoot.Value, storageRootCaseVariant.Value);
        Assert.Equal(primary.LaunchServiceNameSuffix, secondary.LaunchServiceNameSuffix);
        Assert.Equal(primary.NamedPipeAddressSegment, secondary.NamedPipeAddressSegment);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateNamedPipeGenerationAddress_WithDifferentSessionToken_ReturnsDistinctStableNames ()
    {
        var storageRoot = AbsolutePath.Parse(Path.GetFullPath(Path.Combine(".", "sandbox", "Supervisor")));
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
        var storageRoot = AbsolutePath.Parse(Path.GetFullPath(Path.Combine(".", "sandbox", "Supervisor")));

        var cleanupTarget = resolver.ResolveUnixSocketCleanupTargetOrNull(storageRoot);

        if (OperatingSystem.IsWindows())
        {
            Assert.Null(cleanupTarget);
            return;
        }

        Assert.NotNull(cleanupTarget);
        Assert.True(Path.IsPathFullyQualified(cleanupTarget.SocketPath.Value));
        Assert.True(Encoding.UTF8.GetByteCount(cleanupTarget.SocketPath.Value) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveRuntimeEndpoint_WithValidInputs_ReturnsPlatformSpecificEndpoint ()
    {
        var resolver = new SupervisorEndpointResolver();
        var storageRoot = AbsolutePath.Parse(Path.GetFullPath(Path.Combine(".", "sandbox", "Supervisor")));
        var sessionToken = IpcSessionTokenTestFactory.CreateFromDiscriminator(1);

        var endpoint = resolver.ResolveRuntimeEndpoint(storageRoot, sessionToken);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(IpcTransportKind.NamedPipe, endpoint.Contract.TransportKind);
            Assert.Null(endpoint.UnixSocketPath);
            Assert.Equal(
                SupervisorEndpointResolver.CreateNamedPipeGenerationAddress(
                    storageRoot,
                    sessionToken),
                endpoint.Contract.Address);
            return;
        }

        var preferredPath = Path.Combine(storageRoot.Value, ".ucli", "local", "supervisor", "ipc.sock");
        var socketPath = Assert.IsType<AbsolutePath>(endpoint.UnixSocketPath);

        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint.Contract.TransportKind);
        Assert.Equal(socketPath.Value, endpoint.Contract.Address);
        Assert.True(Encoding.UTF8.GetByteCount(socketPath.Value) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);

        if (Encoding.UTF8.GetByteCount(preferredPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            Assert.Equal(preferredPath, socketPath.Value);
            return;
        }

        AssertFallbackPath(socketPath.Value, "ucli-s-");
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
        var storageRoot = AbsolutePath.Parse(Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "ucli-tests",
            new string('a', 140))));

        var endpoint1 = resolver.ResolveRuntimeEndpoint(storageRoot, sessionToken);
        var endpoint2 = resolver.ResolveRuntimeEndpoint(storageRoot, sessionToken);

        var socketPath1 = Assert.IsType<AbsolutePath>(endpoint1.UnixSocketPath);
        var socketPath2 = Assert.IsType<AbsolutePath>(endpoint2.UnixSocketPath);
        Assert.Equal(IpcTransportKind.UnixDomainSocket, endpoint1.Contract.TransportKind);
        Assert.Equal(socketPath1, socketPath2);
        Assert.True(Encoding.UTF8.GetByteCount(socketPath1.Value) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);
        AssertFallbackPath(socketPath1.Value, "ucli-s-");
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

    private static string SwapLetterCase (string value)
    {
        var characters = value.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];
            if (char.IsUpper(character))
            {
                characters[index] = char.ToLowerInvariant(character);
            }
            else if (char.IsLower(character))
            {
                characters[index] = char.ToUpperInvariant(character);
            }
        }

        return new string(characters);
    }
}
