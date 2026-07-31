using System.ComponentModel;
using System.Runtime.InteropServices;
using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Moves one same-directory artifact with an operating-system create-only rename primitive. </summary>
internal static class ArtifactFileCreateOnlyMove
{
    private const int CurrentWorkingDirectory = -100;
    private const uint LinuxNoReplace = 1;

    // NOTE: Linux C libraries do not expose one uniform renameat2 entry point.
    // Invoke the kernel through syscall with the ABI numbers for the supported Linux architectures.
    private const long LinuxX64RenameAt2SystemCallNumber = 316;
    private const long LinuxArm64RenameAt2SystemCallNumber = 276;

    private const uint MacOsExclusive = 0x00000004;

    public static void Move (
        RepositoryArtifactPublicationPaths paths,
        ArtifactPhysicalFileSession source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        source.EnsureStillBound();
        MovePath(
            paths.TemporaryFile.Target,
            paths.DestinationFile.Target);
        source.MoveBindingTo(
            paths.DestinationFile,
            ImmutableArtifactFilePublisher.DestinationFileSubject);
    }

    private static void MovePath (
        AbsolutePath source,
        AbsolutePath destination)
    {
        if (IsWindows())
        {
            MoveWindows(source, destination);
            return;
        }

        if (IsLinux())
        {
            MoveLinux(source.Value, destination.Value);
            return;
        }

        if (IsMacOS())
        {
            MoveMacOs(source.Value, destination.Value);
            return;
        }

        throw new PlatformNotSupportedException(
            "Create-only artifact publication is supported on Windows, Linux, and macOS.");
    }

    private static void MoveWindows (
        AbsolutePath source,
        AbsolutePath destination)
    {
        if (!MoveFile(
                FileSystemNativePathText.FromGuardedPath(source),
                FileSystemNativePathText.FromGuardedPath(destination)))
        {
            throw CreateIOException(
                $"Immutable artifact create-only move failed: {destination.Value}");
        }
    }

    private static void MoveLinux (string source, string destination)
    {
        if (InvokeLinuxRenameAt2SystemCall(
                GetLinuxRenameAt2SystemCallNumber(),
                CurrentWorkingDirectory,
                source,
                CurrentWorkingDirectory,
                destination,
                LinuxNoReplace) != 0)
        {
            throw CreateIOException(
                $"Immutable artifact create-only move failed: {destination}");
        }
    }

    private static long GetLinuxRenameAt2SystemCallNumber ()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => LinuxX64RenameAt2SystemCallNumber,
            Architecture.Arm64 => LinuxArm64RenameAt2SystemCallNumber,
            _ => throw new PlatformNotSupportedException(
                $"Linux create-only artifact publication is not implemented for the {RuntimeInformation.ProcessArchitecture} process architecture."),
        };
    }

    private static void MoveMacOs (string source, string destination)
    {
        if (RenameExclusive(source, destination, MacOsExclusive) != 0)
        {
            throw CreateIOException(
                $"Immutable artifact create-only move failed: {destination}");
        }
    }

    private static IOException CreateIOException (string message)
    {
        var error = Marshal.GetLastWin32Error();
        return new IOException(
            $"{message}. {new Win32Exception(error).Message} (error={error})");
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFile (
        string existingFileName,
        string newFileName);

    [DllImport(
        "libc",
        CallingConvention = CallingConvention.Cdecl,
        SetLastError = true,
        EntryPoint = "syscall")]
    private static extern long InvokeLinuxRenameAt2SystemCall (
        long systemCallNumber,
        int oldDirectoryFileDescriptor,
        string oldPath,
        int newDirectoryFileDescriptor,
        string newPath,
        uint flags);

    [DllImport("libc", SetLastError = true, EntryPoint = "renamex_np")]
    private static extern int RenameExclusive (
        string oldPath,
        string newPath,
        uint flags);
}
