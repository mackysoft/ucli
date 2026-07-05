using System.Runtime.InteropServices;
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
        var filePath = scope.WriteFile("artifact.bin", "content");

        var result = FileSystemNodeClassifier.IsRegularFile(filePath, File.GetAttributes(filePath));

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void IsRegularFile_WhenPathIsDirectory_ReturnsFalse ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "node-directory");
        var directoryPath = scope.CreateDirectory("output");

        var result = FileSystemNodeClassifier.IsRegularFile(directoryPath, File.GetAttributes(directoryPath));

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void IsRegularFile_WhenPathIsSymbolicLink_ReturnsFalse ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-storage", "node-symlink");
        var targetPath = scope.WriteFile("target.txt", "content");
        var symbolicLinkPath = Path.Combine(scope.FullPath, "linked.txt");
        if (!TestSymbolicLinks.TryCreateFile(symbolicLinkPath, targetPath))
        {
            return;
        }

        var result = FileSystemNodeClassifier.IsRegularFile(symbolicLinkPath, File.GetAttributes(symbolicLinkPath));

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
        var fifoPath = Path.Combine(scope.FullPath, "pipe");
        if (!TryCreateFifo(fifoPath))
        {
            return;
        }

        var result = FileSystemNodeClassifier.IsRegularFile(fifoPath, File.GetAttributes(fifoPath));

        Assert.False(result);
    }

    private static bool TryCreateFifo (string path)
    {
        return MkFifo(path, Convert.ToUInt32("600", 8)) == 0;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "mkfifo")]
    private static extern int MkFifo (
        string path,
        uint mode);
}
