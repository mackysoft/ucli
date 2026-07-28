#if !NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
using MackySoft.FileSystem;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Routes physical node identity reads to the current operating-system boundary. </summary>
internal static class FileSystemNodeIdentityReader
{
    /// <summary> Reads a path without following its leaf reparse point. </summary>
    public static FileSystemNodeIdentity ReadPath (
        AbsolutePath path,
        string subject)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        ValidateSubject(subject);
        if (IsWindows())
        {
            return WindowsFileSystemNodeIdentityReader.ReadPath(path, subject);
        }

        if (IsLinux())
        {
            return PosixFileSystemNodeIdentityReader.ReadLinuxPath(path, subject);
        }

        if (IsMacOS())
        {
            return PosixFileSystemNodeIdentityReader.ReadMacOsPath(path, subject);
        }

        throw CreateUnsupportedPlatformException();
    }

    /// <summary> Reads the physical node from an already-open file handle. </summary>
    public static FileSystemNodeIdentity ReadHandle (
        FileStream stream,
        string subject)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        ValidateSubject(subject);
        if (IsWindows())
        {
            return WindowsFileSystemNodeIdentityReader.ReadHandle(
                stream.SafeFileHandle,
                subject);
        }

        if (IsLinux())
        {
            return PosixFileSystemNodeIdentityReader.ReadLinuxHandle(stream, subject);
        }

        if (IsMacOS())
        {
            return PosixFileSystemNodeIdentityReader.ReadMacOsHandle(stream, subject);
        }

        throw CreateUnsupportedPlatformException();
    }

    private static void ValidateSubject (string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException(
                "Filesystem node identity subject must not be empty or whitespace.",
                nameof(subject));
        }
    }

    private static PlatformNotSupportedException CreateUnsupportedPlatformException ()
    {
        return new PlatformNotSupportedException(
            "Physical filesystem node identity is supported on Windows, Linux, and macOS.");
    }

    private static bool IsWindows ()
    {
#if NET8_0_OR_GREATER
        return OperatingSystem.IsWindows();
#else
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
    }

    private static bool IsLinux ()
    {
#if NET8_0_OR_GREATER
        return OperatingSystem.IsLinux();
#else
        return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#endif
    }

    private static bool IsMacOS ()
    {
#if NET8_0_OR_GREATER
        return OperatingSystem.IsMacOS();
#else
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
#endif
    }
}
