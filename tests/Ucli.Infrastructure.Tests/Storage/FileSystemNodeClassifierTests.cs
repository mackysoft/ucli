using System.Runtime.InteropServices;
using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class FileSystemNodeClassifierTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void IsRegularFile_WhenPathIsRegularFile_ReturnsTrue ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "node-regular-file");
        var filePath = AbsolutePath.Parse(scope.WriteFile("artifact.bin", "content"));

        var result = FileSystemNodeClassifier.IsRegularFile(filePath, File.GetAttributes(filePath.Value));

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void IsRegularFile_WhenPathIsDirectory_ReturnsFalse ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "node-directory");
        var directoryPath = AbsolutePath.Parse(scope.CreateDirectory("output"));

        var result = FileSystemNodeClassifier.IsRegularFile(directoryPath, File.GetAttributes(directoryPath.Value));

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void IsRegularFile_WhenPathIsSymbolicLink_ReturnsFalse ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "node-symlink");
        var targetPath = AbsolutePath.Parse(scope.WriteFile("target.txt", "content"));
        var symbolicLinkPath = AbsolutePath.Parse(Path.Combine(scope.FullPath, "linked.txt"));
        if (!TestSymbolicLinks.TryCreateFile(symbolicLinkPath.Value, targetPath.Value))
        {
            return;
        }

        var result = FileSystemNodeClassifier.IsRegularFile(symbolicLinkPath, File.GetAttributes(symbolicLinkPath.Value));

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void IsRegularFile_WhenPathIsFifo_ReturnsFalse ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "node-fifo");
        var fifoPath = AbsolutePath.Parse(Path.Combine(scope.FullPath, "pipe"));
        if (!TryCreateFifo(fifoPath))
        {
            return;
        }

        var result = FileSystemNodeClassifier.IsRegularFile(fifoPath, File.GetAttributes(fifoPath.Value));

        Assert.False(result);
    }

    private static bool TryCreateFifo (AbsolutePath path)
    {
        return MkFifo(path.Value, Convert.ToUInt32("600", 8)) == 0;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "mkfifo")]
    private static extern int MkFifo (
        string path,
        uint mode);
}
