using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class FileSystemAccessBoundaryTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void EnsureSecureDirectoryChain_WhenBoundaryRootIsSymbolicLink_ThrowsIOException ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "secure-chain-symlink-root");
        var targetDirectoryPath = scope.CreateDirectory("target");
        var boundaryRootPath = Path.Combine(scope.FullPath, "locks");
        if (!TestSymbolicLinks.TryCreateDirectory(boundaryRootPath, targetDirectoryPath))
        {
            return;
        }

        var exception = Assert.Throws<IOException>(() =>
        {
            FileSystemAccessBoundary.EnsureSecureDirectoryChain(
                boundaryRootPath,
                Path.Combine(boundaryRootPath, "unity-projects"));
        });

        Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void EnsureSecureDirectoryChain_WhenIntermediateDirectoryIsSymbolicLink_ThrowsIOException ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "secure-chain-symlink-segment");
        var boundaryRootPath = scope.CreateDirectory("locks");
        var targetDirectoryPath = scope.CreateDirectory("target");
        var symbolicLinkPath = Path.Combine(boundaryRootPath, "linked");
        if (!TestSymbolicLinks.TryCreateDirectory(symbolicLinkPath, targetDirectoryPath))
        {
            return;
        }

        var exception = Assert.Throws<IOException>(() =>
        {
            FileSystemAccessBoundary.EnsureSecureDirectoryChain(
                boundaryRootPath,
                Path.Combine(symbolicLinkPath, "leaf"));
        });

        Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(targetDirectoryPath, "leaf")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void EnsureSecureDirectory_WhenDirectoryIsSymbolicLink_ThrowsIOException ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "secure-directory-symlink");
        var targetDirectoryPath = scope.CreateDirectory("target");
        var symbolicLinkPath = Path.Combine(scope.FullPath, "linked");
        if (!TestSymbolicLinks.TryCreateDirectory(symbolicLinkPath, targetDirectoryPath))
        {
            return;
        }

        var exception = Assert.Throws<IOException>(() =>
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(symbolicLinkPath);
        });

        Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void EnsureSecureDirectory_WhenUcliDirectoryIsSymbolicLink_ThrowsBeforeBootstrapWrite ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "secure-ucli-symlink");
        var targetDirectoryPath = scope.CreateDirectory("target");
        var ucliDirectoryPath = Path.Combine(scope.FullPath, ".ucli");
        if (!TestSymbolicLinks.TryCreateDirectory(ucliDirectoryPath, targetDirectoryPath))
        {
            return;
        }

        var exception = Assert.Throws<IOException>(() =>
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(
                Path.Combine(
                    ucliDirectoryPath,
                    "local",
                    "fingerprints",
                    "fingerprint",
                    "artifacts",
                    "build",
                    "run-1"));
        });

        Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(targetDirectoryPath, ".gitignore")));
    }
}
