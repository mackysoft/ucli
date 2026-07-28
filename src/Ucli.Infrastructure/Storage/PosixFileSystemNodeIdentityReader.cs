using System.Buffers;
using System.Runtime.InteropServices;
using MackySoft.FileSystem;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Reads Linux and macOS physical node identities with <c>lstat</c> and <c>fstat</c>. </summary>
internal static class PosixFileSystemNodeIdentityReader
{
    private const int FileStatusBufferSize = 256;
    private const int FileTypeMask = 0xF000;
    private const int RegularFileType = 0x8000;
    private const int DirectoryType = 0x4000;
    private const int SymbolicLinkType = 0xA000;
    private const int LinuxFileModeOffset = 24;
    private const int LinuxArm64FileModeOffset = 16;
    private const int LinuxLinkCountOffset = 16;
    private const int LinuxArm64LinkCountOffset = 20;
    private const int MacOsFileModeOffset = 4;
    private const int MacOsLinkCountOffset = 6;

    public static FileSystemNodeIdentity ReadLinuxPath (
        AbsolutePath path,
        string subject)
    {
        return ReadPath(path, subject, GetLinuxFileStatusLayout());
    }

    public static FileSystemNodeIdentity ReadMacOsPath (
        AbsolutePath path,
        string subject)
    {
        return ReadPath(path, subject, new FileStatusLayout(
            FileModeOffset: MacOsFileModeOffset,
            FileModeBytes: 2,
            LinkCountOffset: MacOsLinkCountOffset,
            LinkCountBytes: 2,
            DeviceBytes: 4));
    }

    public static FileSystemNodeIdentity ReadLinuxHandle (
        FileStream stream,
        string subject)
    {
        return ReadHandle(stream, subject, GetLinuxFileStatusLayout());
    }

    public static FileSystemNodeIdentity ReadMacOsHandle (
        FileStream stream,
        string subject)
    {
        return ReadHandle(stream, subject, new FileStatusLayout(
            FileModeOffset: MacOsFileModeOffset,
            FileModeBytes: 2,
            LinkCountOffset: MacOsLinkCountOffset,
            LinkCountBytes: 2,
            DeviceBytes: 4));
    }

    private static FileSystemNodeIdentity ReadPath (
        AbsolutePath path,
        string subject,
        FileStatusLayout layout)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(FileStatusBufferSize);
        try
        {
            if (LStat(path.Value, buffer) != 0)
            {
                throw CreateIOException(
                    $"{subject} physical node identity could not be inspected: {path.Value}");
            }

            return ParseIdentity(buffer, layout);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static FileSystemNodeIdentity ReadHandle (
        FileStream stream,
        string subject,
        FileStatusLayout layout)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(FileStatusBufferSize);
        try
        {
            if (FStat(stream.SafeFileHandle.DangerousGetHandle(), buffer) != 0)
            {
                throw CreateIOException(
                    $"{subject} open physical node identity could not be inspected");
            }

            return ParseIdentity(buffer, layout);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static FileSystemNodeIdentity ParseIdentity (
        byte[] fileStatus,
        FileStatusLayout layout)
    {
        var mode = layout.FileModeBytes == 2
            ? BitConverter.ToUInt16(fileStatus, layout.FileModeOffset)
            : BitConverter.ToInt32(fileStatus, layout.FileModeOffset);
        var nodeType = mode & FileTypeMask;
        var device = layout.DeviceBytes == 4
            ? BitConverter.ToUInt32(fileStatus, startIndex: 0)
            : BitConverter.ToUInt64(fileStatus, startIndex: 0);
        var linkCount = layout.LinkCountBytes switch
        {
            2 => BitConverter.ToUInt16(fileStatus, layout.LinkCountOffset),
            4 => BitConverter.ToUInt32(fileStatus, layout.LinkCountOffset),
            _ => BitConverter.ToUInt64(fileStatus, layout.LinkCountOffset),
        };
        return new FileSystemNodeIdentity(
            device,
            new FileSystemNodeIdentifier(
                BitConverter.ToUInt64(fileStatus, startIndex: 8),
                High: 0),
            linkCount,
            nodeType == RegularFileType,
            nodeType == DirectoryType,
            nodeType == SymbolicLinkType);
    }

    private static FileStatusLayout GetLinuxFileStatusLayout ()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => new FileStatusLayout(
                FileModeOffset: LinuxFileModeOffset,
                FileModeBytes: 4,
                LinkCountOffset: LinuxLinkCountOffset,
                LinkCountBytes: 8,
                DeviceBytes: 8),
            Architecture.Arm64 => new FileStatusLayout(
                FileModeOffset: LinuxArm64FileModeOffset,
                FileModeBytes: 4,
                LinkCountOffset: LinuxArm64LinkCountOffset,
                LinkCountBytes: 4,
                DeviceBytes: 8),
            _ => throw new PlatformNotSupportedException(
                $"Linux physical node identity is not implemented for the {RuntimeInformation.ProcessArchitecture} process architecture."),
        };
    }

    private static IOException CreateIOException (string message)
    {
        return new IOException($"{message}. errno={Marshal.GetLastWin32Error()}");
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "lstat")]
    private static extern int LStat (
        string path,
        byte[] fileStatus);

    [DllImport("libc", SetLastError = true, EntryPoint = "fstat")]
    private static extern int FStat (
        IntPtr fileDescriptor,
        byte[] fileStatus);

    private readonly record struct FileStatusLayout (
        int FileModeOffset,
        int FileModeBytes,
        int LinkCountOffset,
        int LinkCountBytes,
        int DeviceBytes);
}
