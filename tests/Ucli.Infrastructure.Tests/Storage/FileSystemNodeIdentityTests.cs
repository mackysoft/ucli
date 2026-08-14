using System.Runtime.Versioning;
using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class FileSystemNodeIdentityTests
{
    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("windows")]
    public void ReadPath_OnWindowsWhenLeafIsMissing_ThrowsFileNotFoundExceptionWithPath ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-storage",
            "identity-missing-leaf");
        var path = AbsolutePath.Parse(scope.GetPath("missing.txt"));

        var exception = Assert.Throws<FileNotFoundException>(() =>
            FileSystemNodeIdentityReader.ReadPath(path, "Identity test source"));

        Assert.Equal(path.Value, exception.FileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("windows")]
    public void ReadPath_OnWindowsWhenAncestorIsMissing_ThrowsDirectoryNotFoundException ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-storage",
            "identity-missing-ancestor");
        var path = AbsolutePath.Parse(scope.GetPath("missing/entry.txt"));

        Assert.Throws<DirectoryNotFoundException>(() =>
            FileSystemNodeIdentityReader.ReadPath(path, "Identity test source"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreatePathOpenException_WhenErrorIsFileNotFound_ReturnsFileNotFoundExceptionWithPath ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-storage",
            "identity-file-not-found");
        var path = AbsolutePath.Parse(scope.GetPath("entry.txt"));

        var exception = WindowsFileSystemNodeIdentityReader.CreatePathOpenException(
            path,
            "Identity test source",
            error: 2);

        var fileNotFoundException = Assert.IsType<FileNotFoundException>(exception);
        Assert.Equal(path.Value, fileNotFoundException.FileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreatePathOpenException_WhenErrorIsPathNotFound_ReturnsDirectoryNotFoundException ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-storage",
            "identity-path-not-found");
        var path = AbsolutePath.Parse(scope.GetPath("entry.txt"));

        var exception = WindowsFileSystemNodeIdentityReader.CreatePathOpenException(
            path,
            "Identity test source",
            error: 3);

        Assert.IsType<DirectoryNotFoundException>(exception);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreatePathOpenException_WhenErrorIsSharingViolation_ReturnsExactIOException ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-storage",
            "identity-sharing-violation");
        var path = AbsolutePath.Parse(scope.GetPath("entry.txt"));

        var exception = WindowsFileSystemNodeIdentityReader.CreatePathOpenException(
            path,
            "Identity test source",
            error: 32);

        Assert.IsType<IOException>(exception);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsSamePhysicalNodeAs_WhenUpperIdentifierHalfDiffers_ReturnsFalse ()
    {
        var expected = new FileSystemNodeIdentity(
            VolumeOrDevice: 1,
            NodeIdentifier: new FileSystemNodeIdentifier(Low: 2, High: 3),
            LinkCount: 1,
            Classification: new FileSystemNodeClassification(
                IsRegularFile: true,
                IsDirectory: false,
                IsReparsePoint: false));
        var replacement = expected with
        {
            NodeIdentifier = new FileSystemNodeIdentifier(Low: 2, High: 4),
        };

        Assert.False(expected.IsSamePhysicalNodeAs(replacement));
    }
}
