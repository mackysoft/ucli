using System.Buffers;
using System.Runtime.InteropServices;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Classifies filesystem node kinds that are not fully represented by <see cref="FileAttributes" />. </summary>
internal static class FileSystemNodeClassifier
{
    private const int PosixFileStatusBufferSize = 256;

    private const int PosixFileTypeMask = 0xF000;

    private const int PosixRegularFileType = 0x8000;

    private const int LinuxFileModeOffset = 24;

    private const int LinuxArm64FileModeOffset = 16;

    private const int MacOsFileModeOffset = 4;

    /// <summary> Returns whether the specified filesystem node is a regular file. </summary>
    /// <param name="filePath"> The path to inspect. </param>
    /// <param name="attributes"> The attributes already read for <paramref name="filePath" />. </param>
    /// <returns> <see langword="true" /> when the node is a regular file; otherwise <see langword="false" />. </returns>
    public static bool IsRegularFile (
        string filePath,
        FileAttributes attributes)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must not be empty.", nameof(filePath));
        }

        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
        {
            return false;
        }

        if (IsWindows())
        {
            return (attributes & FileAttributes.Device) == 0;
        }

        if (IsLinux())
        {
            return IsPosixRegularFile(filePath, GetLinuxFileModeOffset(), bytes: 4);
        }

        if (IsMacOS())
        {
            return IsPosixRegularFile(filePath, MacOsFileModeOffset, bytes: 2);
        }

        return true;
    }

    private static int GetLinuxFileModeOffset ()
    {
        return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? LinuxArm64FileModeOffset
            : LinuxFileModeOffset;
    }

    private static bool IsPosixRegularFile (
        string filePath,
        int fileModeOffset,
        int bytes)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(PosixFileStatusBufferSize);
        try
        {
            if (LStat(filePath, buffer) != 0)
            {
                throw new IOException($"File type could not be inspected: {filePath}. errno={Marshal.GetLastWin32Error()}");
            }

            var mode = bytes == 2
                ? BitConverter.ToUInt16(buffer, fileModeOffset)
                : BitConverter.ToInt32(buffer, fileModeOffset);
            return (mode & PosixFileTypeMask) == PosixRegularFileType;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
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

    [DllImport("libc", SetLastError = true, EntryPoint = "lstat")]
    private static extern int LStat (
        string path,
        byte[] fileStatus);
}
