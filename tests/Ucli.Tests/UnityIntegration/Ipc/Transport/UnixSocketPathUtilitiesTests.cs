using System.Text;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnixSocketPathUtilitiesTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ValidateSocketPathLength_WithPathAtSharedLimit_DoesNotThrow ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var socketPath = CreateSocketPathWithByteLength(IpcTransportConstraints.UnixDomainSocketPathMaxBytes);

        UnixSocketPathUtilities.ValidateSocketPathLength(socketPath, "address");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ValidateSocketPathLength_WithPathExceedingSharedLimit_ThrowsArgumentException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var socketPath = CreateSocketPathWithByteLength(IpcTransportConstraints.UnixDomainSocketPathMaxBytes + 1);

        var exception = Assert.Throws<ArgumentException>(() => UnixSocketPathUtilities.ValidateSocketPathLength(socketPath, "address"));

        Assert.Equal("address", exception!.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildFallbackSocketPath_WhenResolved_ReturnsPathWithinSharedLimit ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var socketPath = UnixSocketPathUtilities.BuildFallbackSocketPath(
            UcliIpcEndpointNames.DaemonAddressPrefix,
            new string('a', 256));

        Assert.True(Encoding.UTF8.GetByteCount(socketPath) <= IpcTransportConstraints.UnixDomainSocketPathMaxBytes);
    }

    private static string CreateSocketPathWithByteLength (int totalBytes)
    {
        var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var basePath = Path.Combine(tempRoot, "ucli-test-", UcliIpcEndpointNames.UnixSocketFileName);
        var additionalBytes = totalBytes - Encoding.UTF8.GetByteCount(basePath);
        Assert.True(additionalBytes >= 0);
        return Path.Combine(
            tempRoot,
            "ucli-test-" + new string('a', additionalBytes),
            UcliIpcEndpointNames.UnixSocketFileName);
    }
}
