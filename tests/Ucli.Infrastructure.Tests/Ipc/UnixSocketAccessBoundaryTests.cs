using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Infrastructure.Tests.Ipc;

public sealed class UnixSocketAccessBoundaryTests
{
    private const UnixFileMode OwnerOnlyDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode SharedDirectoryMode =
        OwnerOnlyDirectoryMode |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenAuthorizedPathIsRelative_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new UnixSocketAccessBoundary("relative/ipc.sock"));

        Assert.Equal("authorizedSocketPath", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void PrepareForBind_WithAuthorizedPath_SecuresDirectoryAndDeletesStaleNode ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("infrastructure-ipc", "local-storage-socket-boundary");
        var socketDirectoryPath = Path.Combine(
            scope.FullPath,
            ".ucli",
            "local",
            "fingerprints",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        Directory.CreateDirectory(socketDirectoryPath);
        var socketPath = Path.Combine(socketDirectoryPath, UcliIpcEndpointNames.UnixSocketFileName);
        File.WriteAllText(socketPath, "stale");
        File.SetUnixFileMode(socketDirectoryPath, SharedDirectoryMode);

        var boundary = new UnixSocketAccessBoundary(socketPath);

        boundary.PrepareForBind();

        Assert.False(File.Exists(socketPath));
        Assert.Equal(OwnerOnlyDirectoryMode, File.GetUnixFileMode(socketDirectoryPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Cleanup_WithFallbackPath_RemovesSocketNodeButPreservesStableDirectory ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("infrastructure-ipc", "fallback-socket-boundary");
        var fallbackPath = new UnixSocketFallbackPath(
            Path.GetTempPath(),
            UnixSocketFallbackPurpose.Daemon,
            $"{scope.FullPath}\n{Guid.NewGuid():N}");
        Directory.CreateDirectory(fallbackPath.DirectoryPath);
        File.WriteAllText(fallbackPath.SocketPath, "stale");
        var boundary = new UnixSocketAccessBoundary(fallbackPath.SocketPath);

        try
        {
            boundary.Cleanup();

            Assert.False(File.Exists(fallbackPath.SocketPath));
            Assert.True(Directory.Exists(fallbackPath.DirectoryPath));
        }
        finally
        {
            if (Directory.Exists(fallbackPath.DirectoryPath))
            {
                Directory.Delete(fallbackPath.DirectoryPath, recursive: true);
            }
        }
    }
}
