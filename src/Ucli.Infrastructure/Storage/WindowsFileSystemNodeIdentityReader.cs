using System.ComponentModel;
using System.Runtime.InteropServices;
using MackySoft.FileSystem;
using Microsoft.Win32.SafeHandles;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Reads Windows physical node identities through metadata-only node handles. </summary>
internal static class WindowsFileSystemNodeIdentityReader
{
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;

    public static FileSystemNodeIdentity ReadPath (
        AbsolutePath path,
        string subject)
    {
        using var handle = CreateFile(
            path.Value,
            desiredAccess: 0,
            FileShare.Read | FileShare.Write | FileShare.Delete,
            securityAttributes: IntPtr.Zero,
            OpenExisting,
            BackupSemantics | OpenReparsePoint,
            templateFile: IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw CreateIOException(
                $"{subject} physical node could not be opened for identity inspection: {path.Value}");
        }

        return ReadHandle(handle, subject);
    }

    public static FileSystemNodeIdentity ReadHandle (
        SafeFileHandle handle,
        string subject)
    {
        if (!GetFileInformationByHandle(handle, out var information))
        {
            throw CreateIOException(
                $"{subject} physical node identity could not be inspected");
        }

        var attributes = information.fileAttributes;
        return new FileSystemNodeIdentity(
            information.volumeSerialNumber,
            ((ulong)information.fileIndexHigh << 32) | information.fileIndexLow,
            information.numberOfLinks,
            IsRegularFile(attributes),
            (attributes & FileAttributes.Directory) != 0,
            (attributes & FileAttributes.ReparsePoint) != 0);
    }

    private static bool IsRegularFile (FileAttributes attributes)
    {
        return (attributes
            & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) == 0;
    }

    private static IOException CreateIOException (string message)
    {
        var error = Marshal.GetLastWin32Error();
        return new IOException(
            $"{message}. {new Win32Exception(error).Message} (error={error})");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile (
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle (
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public FileAttributes fileAttributes;
        public WindowsFileTime creationTime;
        public WindowsFileTime lastAccessTime;
        public WindowsFileTime lastWriteTime;
        public uint volumeSerialNumber;
        public uint fileSizeHigh;
        public uint fileSizeLow;
        public uint numberOfLinks;
        public uint fileIndexHigh;
        public uint fileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsFileTime
    {
        public uint lowDateTime;
        public uint highDateTime;
    }
}
