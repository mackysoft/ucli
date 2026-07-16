using System.Security.Cryptography;
using System.Text;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc.Bootstrap;

public sealed class UnixSocketFallbackPathTests
{
    private const string IdentitySource = "project-identity";

    public static TheoryData<int, string> PurposePrefixes => new()
    {
        { (int)UnixSocketFallbackPurpose.Daemon, "ucli-d-" },
        { (int)UnixSocketFallbackPurpose.GuiSupervisor, "ucli-g-" },
        { (int)UnixSocketFallbackPurpose.Supervisor, "ucli-s-" },
        { (int)UnixSocketFallbackPurpose.SupervisorGeneration, "ucli-sg-" },
        { (int)UnixSocketFallbackPurpose.SupervisorPublicationLock, "ucli-sl-" },
        { (int)UnixSocketFallbackPurpose.ListenerOwnershipLock, "ucli-il-" },
    };

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_ExposesPurposeInsteadOfCallerSuppliedDirectoryPrefix ()
    {
        var constructor = Assert.Single(typeof(UnixSocketFallbackPath).GetConstructors());
        var parameters = constructor.GetParameters();

        Assert.Collection(
            parameters,
            parameter => Assert.Equal(typeof(string), parameter.ParameterType),
            parameter => Assert.Equal(typeof(UnixSocketFallbackPurpose), parameter.ParameterType),
            parameter => Assert.Equal(typeof(string), parameter.ParameterType));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InfrastructureAssembly_DoesNotExposeArbitraryPrefixSocketPathUtility ()
    {
        var removedUtilityType = typeof(UnixSocketFallbackPath).Assembly.GetType(
            "MackySoft.Ucli.Infrastructure.Ipc.UnixSocketPathUtilities");

        Assert.Null(removedUtilityType);
    }

    [Theory]
    [MemberData(nameof(PurposePrefixes))]
    [Trait("Size", "Small")]
    public void Constructor_WithDefinedPurpose_BuildsDeterministic128BitFallbackPath (
        int purposeValue,
        string expectedDirectoryPrefix)
    {
        var purpose = (UnixSocketFallbackPurpose)purposeValue;
        var temporaryDirectoryPath = Path.GetFullPath(Path.GetTempPath());
        var expectedIdentityHex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(IdentitySource)))
            .ToLowerInvariant()[..32];

        var fallbackPath = new UnixSocketFallbackPath(
            temporaryDirectoryPath,
            purpose,
            IdentitySource);
        var sameValue = new UnixSocketFallbackPath(
            temporaryDirectoryPath,
            purpose,
            IdentitySource);

        Assert.Equal(purpose, fallbackPath.Purpose);
        Assert.Equal(
            expectedDirectoryPrefix + expectedIdentityHex,
            Path.GetFileName(fallbackPath.DirectoryPath));
        Assert.Equal(
            Path.Combine(fallbackPath.DirectoryPath, UcliIpcEndpointNames.UnixSocketFileName),
            fallbackPath.SocketPath);
        Assert.True(
            Encoding.UTF8.GetByteCount(fallbackPath.SocketPath)
            <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);
        Assert.Equal(fallbackPath, sameValue);
        Assert.Equal(fallbackPath.GetHashCode(), sameValue.GetHashCode());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithUndefinedPurpose_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new UnixSocketFallbackPath(
            Path.GetTempPath(),
            (UnixSocketFallbackPurpose)int.MaxValue,
            IdentitySource));

        Assert.Equal("purpose", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithRelativeTemporaryDirectory_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new UnixSocketFallbackPath(
            "relative-temp",
            UnixSocketFallbackPurpose.Daemon,
            IdentitySource));

        Assert.Equal("temporaryDirectoryPath", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public void Constructor_WithEmptyTemporaryDirectory_ThrowsArgumentException (string? temporaryDirectoryPath)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new UnixSocketFallbackPath(
            temporaryDirectoryPath!,
            UnixSocketFallbackPurpose.Daemon,
            IdentitySource));

        Assert.Equal("temporaryDirectoryPath", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [Trait("Size", "Small")]
    public void Constructor_WithEmptyIdentitySource_ThrowsArgumentException (string? identitySource)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new UnixSocketFallbackPath(
            Path.GetTempPath(),
            UnixSocketFallbackPurpose.Daemon,
            identitySource!));

        Assert.Equal("identitySource", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenTemporaryRootCannotRetain128BitIdentity_ThrowsWithoutCreatingDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.GetPathRoot(Path.GetTempPath())!;
        var temporaryDirectoryPath = Path.Combine(
            root,
            "ucli-fallback-length-" + new string('t', IpcTransportConstraints.UnixDomainSocketPathMaxBytes));

        var exception = Assert.Throws<InvalidOperationException>(() => new UnixSocketFallbackPath(
            temporaryDirectoryPath,
            UnixSocketFallbackPurpose.Daemon,
            IdentitySource));

        Assert.Contains("required 128-bit endpoint identity", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(temporaryDirectoryPath));
    }

    [Theory]
    [InlineData("ucli-sg-0123456789abcdef0123456789abcdef", true)]
    [InlineData("ucli-sg-0123456789abcdef0123456789abcde", false)]
    [InlineData("ucli-sg-0123456789ABCDEF0123456789ABCDEF", false)]
    [InlineData("ucli-s-0123456789abcdef0123456789abcdef", false)]
    [InlineData(null, false)]
    [Trait("Size", "Small")]
    public void IsDirectoryNameForPurpose_RequiresExactPurposeBoundLowerHexShape (
        string? directoryName,
        bool expected)
    {
        var actual = UnixSocketFallbackPath.IsDirectoryNameForPurpose(
            directoryName,
            UnixSocketFallbackPurpose.SupervisorGeneration);

        Assert.Equal(expected, actual);
    }
}
