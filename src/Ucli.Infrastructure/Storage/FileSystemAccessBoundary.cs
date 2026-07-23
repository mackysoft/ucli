using System.Diagnostics.CodeAnalysis;
#if !NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
#if NET8_0_OR_GREATER
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
#endif
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Applies same-user filesystem boundaries to local runtime storage. </summary>
internal static class FileSystemAccessBoundary
{
#if NET8_0_OR_GREATER
    private const UnixFileMode OwnerOnlyDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode OwnerOnlyFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;
#else
    private const int OwnerOnlyDirectoryMode = 0x1C0;

    private const int OwnerOnlyFileMode = 0x180;
#endif

    /// <summary> Ensures the guarded target directory exists and is limited to the current user. </summary>
    public static void EnsureSecureDirectory (AbsolutePath directoryPath)
    {
        if (TryResolveLocalDirectoryRoot(directoryPath, out var localDirectoryRoot))
        {
            UcliLocalStorageBootstrapper.EnsureInitialized(directoryPath);
            EnsureSecureDirectoryChainCore(ContainedPath.Create(localDirectoryRoot!, directoryPath));
            return;
        }

        Directory.CreateDirectory(directoryPath.Value);
        EnsureSecureDirectoryNode(directoryPath);
    }

    /// <summary> Ensures the guarded target directory chain is limited to the current user from its owned boundary root. </summary>
    public static void EnsureSecureDirectoryChain (ContainedPath directory)
    {
        if (directory is null)
        {
            throw new ArgumentNullException(nameof(directory));
        }

        EnsureSecureDirectoryChainCore(directory);
    }

    /// <summary> Ensures the guarded target file is limited to the current user. </summary>
    public static void EnsureSecureFile (AbsolutePath filePath)
    {
        FileUtilities.EnsureRegularFile(filePath, "Secure file target");
        ApplySecureFileMode(filePath);
    }

    /// <summary> Ensures the target unix-domain-socket node is limited to the current user. </summary>
    /// <param name="socketPath"> The socket path to secure. </param>
    public static void EnsureSecureUnixSocket (AbsolutePath socketPath)
    {
        if (socketPath is null)
        {
            throw new ArgumentNullException(nameof(socketPath));
        }

        if (!File.Exists(socketPath.Value))
        {
            throw new FileNotFoundException(
                $"Secure socket target was not found: {socketPath}",
                socketPath.Value);
        }

        ApplySecureFileMode(socketPath);
    }

    private static void EnsureSecureDirectoryChainCore (ContainedPath directory)
    {
        var pendingDirectories = new Stack<AbsolutePath>();
        var currentDirectoryPath = directory.Target;
        while (true)
        {
            pendingDirectories.Push(currentDirectoryPath);
            if (currentDirectoryPath.IsSameAs(directory.BoundaryRoot))
            {
                break;
            }

            var childDirectoryPath = currentDirectoryPath;
            if (!childDirectoryPath.TryGetParent(out var parentDirectoryPath))
            {
                throw new InvalidOperationException(
                    $"Parent directory path could not be resolved: {childDirectoryPath}");
            }

            currentDirectoryPath = parentDirectoryPath;
        }

        while (pendingDirectories.Count > 0)
        {
            var currentPath = pendingDirectories.Pop();
            Directory.CreateDirectory(currentPath.Value);
            EnsureSecureDirectoryNode(currentPath);
        }
    }

    private static void EnsureSecureDirectoryNode (AbsolutePath directoryPath)
    {
        EnsureDirectoryIsNotReparsePoint(directoryPath);
        ApplySecureDirectoryMode(directoryPath);
    }

    private static void EnsureDirectoryIsNotReparsePoint (AbsolutePath directoryPath)
    {
        var attributes = File.GetAttributes(directoryPath.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Secure directory target must not be a reparse point: {directoryPath}");
        }
    }

    private static void ApplySecureDirectoryMode (AbsolutePath directoryPath)
    {
#if NET8_0_OR_GREATER
        if (OperatingSystem.IsWindows())
        {
            ApplyCurrentUserDirectoryAcl(directoryPath);
            return;
        }

        File.SetUnixFileMode(directoryPath.Value, OwnerOnlyDirectoryMode);
#else
        if (IsWindows())
        {
            return;
        }

        ApplyUnixFileMode(directoryPath, OwnerOnlyDirectoryMode, "directory");
#endif
    }

    private static void ApplySecureFileMode (AbsolutePath filePath)
    {
#if NET8_0_OR_GREATER
        if (OperatingSystem.IsWindows())
        {
            ApplyCurrentUserFileAcl(filePath);
            return;
        }

        File.SetUnixFileMode(filePath.Value, OwnerOnlyFileMode);
#else
        if (IsWindows())
        {
            return;
        }

        ApplyUnixFileMode(filePath, OwnerOnlyFileMode, "file");
#endif
    }

#if NET8_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    private static void ApplyCurrentUserDirectoryAcl (AbsolutePath directoryPath)
    {
        var directorySecurity = new DirectorySecurity();
        directorySecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        directorySecurity.AddAccessRule(new FileSystemAccessRule(
            GetCurrentUserSid(),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        new DirectoryInfo(directoryPath.Value).SetAccessControl(directorySecurity);
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyCurrentUserFileAcl (AbsolutePath filePath)
    {
        var fileSecurity = new FileSecurity();
        fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        fileSecurity.AddAccessRule(new FileSystemAccessRule(
            GetCurrentUserSid(),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        new FileInfo(filePath.Value).SetAccessControl(fileSecurity);
    }

    [SupportedOSPlatform("windows")]
    private static SecurityIdentifier GetCurrentUserSid ()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User
            ?? throw new InvalidOperationException("Current Windows user SID could not be resolved for filesystem access boundary.");
    }
#else
    private static void ApplyUnixFileMode (
        AbsolutePath path,
        int mode,
        string targetKind)
    {
        if (Chmod(path.Value, mode) != 0)
        {
            throw new IOException($"chmod failed for {targetKind} '{path}'. errno={Marshal.GetLastWin32Error()}");
        }
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "chmod")]
    private static extern int Chmod (string pathname, int mode);
#endif

    private static bool TryResolveLocalDirectoryRoot (
        AbsolutePath directoryPath,
        [NotNullWhen(true)] out AbsolutePath? localDirectoryRoot)
    {
        var currentPath = directoryPath;
        while (currentPath.TryGetParent(out var parentPath))
        {
            if (parentPath.TryGetParent(out var storagePath))
            {
                var expectedUcliPath = ContainedPath.Create(
                    storagePath,
                    RootRelativePath.Parse(UcliStoragePathNames.UcliDirectoryName)).Target;
                var expectedLocalPath = ContainedPath.Create(
                    parentPath,
                    RootRelativePath.Parse(UcliStoragePathNames.LocalDirectoryName)).Target;
                if (parentPath.IsSameAs(expectedUcliPath)
                    && currentPath.IsSameAs(expectedLocalPath))
                {
                    localDirectoryRoot = currentPath;
                    return true;
                }
            }

            currentPath = parentPath;
        }

        localDirectoryRoot = null;
        return false;
    }

    private static bool IsWindows ()
    {
#if NET8_0_OR_GREATER
        return OperatingSystem.IsWindows();
#else
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
    }
}
