using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc;

public sealed class UnixSocketEndpointPathPolicyTests
{
    [Theory]
    [InlineData(null, PathValidationFailureKind.EmptyPath)]
    [InlineData("", PathValidationFailureKind.EmptyPath)]
    [InlineData(" ", PathValidationFailureKind.ExpectedAbsolutePath)]
    [InlineData("relative.sock", PathValidationFailureKind.ExpectedAbsolutePath)]
    [Trait("Size", "Small")]
    public void Parse_WithInvalidAbsolutePath_ReturnsFactoryFailure (
        string? address,
        PathValidationFailureKind expectedFailureKind)
    {
        var exception = Assert.Throws<PathValidationException>(() =>
            UnixSocketEndpointPathPolicy.Parse(address!));

        Assert.Equal(expectedFailureKind, exception.Failure.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WithFilesystemRoot_RejectsTransportAddress ()
    {
        var root = AbsolutePath.Parse(Path.GetFullPath(".")).GetRoot();

        var exception = Assert.Throws<ArgumentException>(() =>
            UnixSocketEndpointPathPolicy.Parse(root.Value));

        Assert.Equal("path", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WithAddressExceedingTransportLimit_RejectsGuardedPath ()
    {
        var address = Path.Combine(
            Path.GetTempPath(),
            new string('a', IpcTransportConstraints.UnixDomainSocketPathMaxBytes + 1));

        var exception = Assert.Throws<ArgumentException>(() =>
            UnixSocketEndpointPathPolicy.Parse(address));

        Assert.Equal("path", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WithLexicalSegments_NormalizesOnceAtRuntimeBoundary ()
    {
        var address = Path.Combine(
            Path.GetTempPath(),
            "ipc-policy",
            "nested",
            "..",
            "socket");
        var expected = AbsolutePath.Parse(
            Path.Combine(Path.GetTempPath(), "ipc-policy", "socket"));

        var path = UnixSocketEndpointPathPolicy.Parse(address);

        Assert.Equal(expected, path);
        Assert.Equal(expected.Value, path.Value);
    }
}
