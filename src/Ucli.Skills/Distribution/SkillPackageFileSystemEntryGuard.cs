using System.Runtime.InteropServices;

namespace MackySoft.Ucli.Skills.Distribution;

/// <summary> Validates file-system entry types before reading package content. </summary>
internal static class SkillPackageFileSystemEntryGuard
{
    private const ushort FileTypeMask = 0xF000;
    private const ushort DirectoryFileType = 0x4000;
    private const ushort RegularFileType = 0x8000;

    private const int AtCurrentWorkingDirectory = -100;
    private const int AtSymlinkNoFollow = 0x100;
    private const uint StatxType = 0x0001;
    private const int StatBufferSize = 512;
    private const int LinuxStatxModeOffset = 28;
    private const int DarwinStatModeOffset = 4;

    /// <summary> Returns whether <paramref name="path" /> is a regular file and not a link or device entry. </summary>
    /// <param name="path"> The file-system path to inspect. </param>
    /// <returns> <see langword="true" /> when the entry is a regular file. </returns>
    public static bool IsRegularFile (string path)
    {
        return IsSupportedEntryType(
            path,
            static attributes => (attributes & (FileAttributes.Directory | FileAttributes.Device | FileAttributes.ReparsePoint)) == 0,
            RegularFileType);
    }

    /// <summary> Returns whether <paramref name="path" /> is a directory and not a link or device entry. </summary>
    /// <param name="path"> The file-system path to inspect. </param>
    /// <returns> <see langword="true" /> when the entry is a directory. </returns>
    public static bool IsDirectory (string path)
    {
        return IsSupportedEntryType(
            path,
            static attributes => (attributes & FileAttributes.Directory) != 0
                && (attributes & (FileAttributes.Device | FileAttributes.ReparsePoint)) == 0,
            DirectoryFileType);
    }

    private static bool IsSupportedEntryType (
        string path,
        Func<FileAttributes, bool> isSupportedAttributes,
        ushort expectedUnixFileType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var attributes = File.GetAttributes(path);
            if (!isSupportedAttributes(attributes))
            {
                return false;
            }

            if (OperatingSystem.IsLinux())
            {
                return TryGetLinuxFileMode(path, out var mode) && IsModeType(mode, expectedUnixFileType);
            }

            if (OperatingSystem.IsMacOS())
            {
                return TryGetDarwinFileMode(path, out var mode) && IsModeType(mode, expectedUnixFileType);
            }

            return OperatingSystem.IsWindows();
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (MarshalDirectiveException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsModeType (
        ushort mode,
        ushort expectedFileType)
    {
        return (mode & FileTypeMask) == expectedFileType;
    }

    private static bool TryGetLinuxFileMode (
        string path,
        out ushort mode)
    {
        var buffer = new byte[StatBufferSize];
        if (Statx(AtCurrentWorkingDirectory, path, AtSymlinkNoFollow, StatxType, buffer) != 0)
        {
            mode = 0;
            return false;
        }

        mode = BitConverter.ToUInt16(buffer, LinuxStatxModeOffset);
        return true;
    }

    private static bool TryGetDarwinFileMode (
        string path,
        out ushort mode)
    {
        var buffer = new byte[StatBufferSize];
        if (LStat(path, buffer) != 0)
        {
            mode = 0;
            return false;
        }

        mode = BitConverter.ToUInt16(buffer, DarwinStatModeOffset);
        return true;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "statx")]
    private static extern int Statx (
        int directoryFileDescriptor,

        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags,
        uint mask,
        byte[] buffer);

    [DllImport("libc", SetLastError = true, EntryPoint = "lstat")]
    private static extern int LStat (
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        byte[] buffer);
}
