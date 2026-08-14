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

    private const int ErrorFileNotFound = 2;

    private const int ErrorPathNotFound = 3;

    public static FileSystemNodeIdentity ReadPath (
        AbsolutePath path,
        string subject)
    {
        using var handle = CreateFile(
            FileSystemNativePathText.FromGuardedPath(path),
            desiredAccess: 0,
            FileShare.Read | FileShare.Write | FileShare.Delete,
            securityAttributes: IntPtr.Zero,
            OpenExisting,
            BackupSemantics | OpenReparsePoint,
            templateFile: IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            throw CreatePathOpenException(path, subject, error);
        }

        return ReadHandle(handle, subject);
    }

    public static FileSystemNodeIdentity ReadHandle (
        SafeFileHandle handle,
        string subject)
    {
        // FILE_ID_INFO owns binding identity because the legacy 64-bit file index is not unique on ReFS.
        // The legacy call remains the source of node kind and link count, which FILE_ID_INFO does not expose.
        if (!GetFileInformationByHandle(handle, out var nodeInformation))
        {
            throw CreateIOException(
                $"{subject} physical node metadata could not be inspected");
        }

        if (!GetFileInformationByHandleEx(
                handle,
                FileInfoByHandleClass.FileIdInfo,
                out var identityInformation,
                checked((uint)Marshal.SizeOf<FileIdInformation>())))
        {
            throw CreateIOException(
                $"{subject} physical node identity could not be inspected");
        }

        var attributes = nodeInformation.fileAttributes;
        return new FileSystemNodeIdentity(
            identityInformation.volumeSerialNumber,
            new FileSystemNodeIdentifier(
                identityInformation.fileId.low,
                identityInformation.fileId.high),
            nodeInformation.numberOfLinks,
            new FileSystemNodeClassification(
                IsRegularFile(attributes),
                (attributes & FileAttributes.Directory) != 0,
                (attributes & FileAttributes.ReparsePoint) != 0));
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

    private static IOException CreatePathOpenException (
        AbsolutePath path,
        string subject,
        int error)
    {
        var message =
            $"{subject} physical node could not be opened for identity inspection: {path.Value}";
        return error switch
        {
            ErrorFileNotFound => new FileNotFoundException(
                $"{message}. {new Win32Exception(error).Message} (error={error})",
                path.Value),
            ErrorPathNotFound => new DirectoryNotFoundException(
                $"{message}. {new Win32Exception(error).Message} (error={error})"),
            _ => new IOException(
                $"{message}. {new Win32Exception(error).Message} (error={error})"),
        };
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx (
        SafeFileHandle file,
        FileInfoByHandleClass fileInformationClass,
        out FileIdInformation fileInformation,
        uint bufferSize);

    private enum FileInfoByHandleClass
    {
        FileIdInfo = 18,
    }

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
    private struct FileIdInformation
    {
        public ulong volumeSerialNumber;
        public FileId128 fileId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileId128
    {
        public ulong low;
        public ulong high;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsFileTime
    {
        public uint lowDateTime;
        public uint highDateTime;
    }
}
