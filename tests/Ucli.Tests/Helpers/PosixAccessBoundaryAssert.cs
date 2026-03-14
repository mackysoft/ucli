using System.Runtime.Versioning;

namespace MackySoft.Ucli.Tests.Helpers;

internal static class PosixAccessBoundaryAssert
{
    private const UnixFileMode OwnerOnlyDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode OwnerOnlyFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public static void DirectoryIsOwnerOnly (string path)
    {
        Assert.Equal(OwnerOnlyDirectoryMode, File.GetUnixFileMode(path));
    }

    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public static void FileIsOwnerOnly (string path)
    {
        Assert.Equal(OwnerOnlyFileMode, File.GetUnixFileMode(path));
    }
}