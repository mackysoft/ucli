#if !NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
#if NET8_0_OR_GREATER
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
#endif
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

    /// <summary> Ensures the target directory exists and is limited to the current user. </summary>
    /// <param name="directoryPath"> The directory path to secure. </param>
    public static void EnsureSecureDirectory (string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path must not be empty.", nameof(directoryPath));
        }

        var normalizedDirectoryPath = Path.GetFullPath(directoryPath);
        if (TryResolveLocalDirectoryRoot(normalizedDirectoryPath, out var localDirectoryRoot))
        {
            UcliLocalStorageBootstrapper.EnsureInitialized(normalizedDirectoryPath);
            EnsureSecureDirectoryChain(localDirectoryRoot!, normalizedDirectoryPath);
            return;
        }

        Directory.CreateDirectory(normalizedDirectoryPath);
        ApplySecureDirectoryMode(normalizedDirectoryPath);
    }

    /// <summary> Ensures the target file is limited to the current user. </summary>
    /// <param name="filePath"> The file path to secure. </param>
    public static void EnsureSecureFile (string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must not be empty.", nameof(filePath));
        }

        var normalizedFilePath = Path.GetFullPath(filePath);
        if (!File.Exists(normalizedFilePath))
        {
            throw new FileNotFoundException($"Secure file target was not found: {normalizedFilePath}", normalizedFilePath);
        }

        ApplySecureFileMode(normalizedFilePath);
    }

    /// <summary> Ensures the target unix-domain-socket node is limited to the current user. </summary>
    /// <param name="socketPath"> The socket path to secure. </param>
    public static void EnsureSecureUnixSocket (string socketPath)
    {
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            throw new ArgumentException("Socket path must not be empty.", nameof(socketPath));
        }

        var normalizedSocketPath = Path.GetFullPath(socketPath);
        if (!File.Exists(normalizedSocketPath))
        {
            throw new FileNotFoundException($"Secure socket target was not found: {normalizedSocketPath}", normalizedSocketPath);
        }

        ApplySecureFileMode(normalizedSocketPath);
    }

    private static void EnsureSecureDirectoryChain (
        string boundaryRootPath,
        string directoryPath)
    {
        var normalizedBoundaryRootPath = NormalizeDirectoryPath(boundaryRootPath);
        var normalizedDirectoryPath = NormalizeDirectoryPath(directoryPath);
        if (!IsPathUnderOrEqual(normalizedDirectoryPath, normalizedBoundaryRootPath))
        {
            throw new InvalidOperationException(
                $"Secure directory target must remain under the local storage root. Root={normalizedBoundaryRootPath}, Target={normalizedDirectoryPath}");
        }

        var pendingDirectories = new Stack<string>();
        var currentDirectoryPath = normalizedDirectoryPath;
        while (true)
        {
            pendingDirectories.Push(currentDirectoryPath);
            if (string.Equals(currentDirectoryPath, normalizedBoundaryRootPath, GetPathComparison()))
            {
                break;
            }

            currentDirectoryPath = Path.GetDirectoryName(currentDirectoryPath)
                ?? throw new InvalidOperationException($"Parent directory path could not be resolved: {currentDirectoryPath}");
        }

        while (pendingDirectories.Count > 0)
        {
            var currentPath = pendingDirectories.Pop();
            Directory.CreateDirectory(currentPath);
            ApplySecureDirectoryMode(currentPath);
        }
    }

    private static void ApplySecureDirectoryMode (string directoryPath)
    {
#if NET8_0_OR_GREATER
        if (OperatingSystem.IsWindows())
        {
            ApplyCurrentUserDirectoryAcl(directoryPath);
            return;
        }

        File.SetUnixFileMode(directoryPath, OwnerOnlyDirectoryMode);
#else
        if (IsWindows())
        {
            return;
        }

        ApplyUnixFileMode(directoryPath, OwnerOnlyDirectoryMode, "directory");
#endif
    }

    private static void ApplySecureFileMode (string filePath)
    {
#if NET8_0_OR_GREATER
        if (OperatingSystem.IsWindows())
        {
            ApplyCurrentUserFileAcl(filePath);
            return;
        }

        File.SetUnixFileMode(filePath, OwnerOnlyFileMode);
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
    private static void ApplyCurrentUserDirectoryAcl (string directoryPath)
    {
        var directorySecurity = new DirectorySecurity();
        directorySecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        directorySecurity.AddAccessRule(new FileSystemAccessRule(
            GetCurrentUserSid(),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        new DirectoryInfo(directoryPath).SetAccessControl(directorySecurity);
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyCurrentUserFileAcl (string filePath)
    {
        var fileSecurity = new FileSecurity();
        fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        fileSecurity.AddAccessRule(new FileSystemAccessRule(
            GetCurrentUserSid(),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        new FileInfo(filePath).SetAccessControl(fileSecurity);
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
        string path,
        int mode,
        string targetKind)
    {
        if (Chmod(path, mode) != 0)
        {
            throw new IOException($"chmod failed for {targetKind} '{path}'. errno={Marshal.GetLastWin32Error()}");
        }
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "chmod")]
    private static extern int Chmod (string pathname, int mode);
#endif

    private static bool TryResolveLocalDirectoryRoot (
        string directoryPath,
        out string? localDirectoryRoot)
    {
        var currentDirectory = new DirectoryInfo(Path.GetFullPath(directoryPath));
        while (currentDirectory != null)
        {
            var parentDirectory = currentDirectory.Parent;
            if (string.Equals(currentDirectory.Name, UcliStoragePathNames.LocalDirectoryName, GetPathComparison())
                && parentDirectory != null
                && string.Equals(parentDirectory.Name, UcliStoragePathNames.UcliDirectoryName, GetPathComparison()))
            {
                localDirectoryRoot = currentDirectory.FullName;
                return true;
            }

            currentDirectory = parentDirectory;
        }

        localDirectoryRoot = null;
        return false;
    }

    private static bool IsPathUnderOrEqual (
        string path,
        string rootPath)
    {
        if (string.Equals(path, rootPath, GetPathComparison()))
        {
            return true;
        }

        var normalizedRootPath = EnsureTrailingDirectorySeparator(rootPath);
        return EnsureTrailingDirectorySeparator(path).StartsWith(normalizedRootPath, GetPathComparison());
    }

    private static string NormalizeDirectoryPath (string directoryPath)
    {
        return Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string EnsureTrailingDirectorySeparator (string directoryPath)
    {
        return directoryPath.EndsWith(Path.DirectorySeparatorChar)
            ? directoryPath
            : directoryPath + Path.DirectorySeparatorChar;
    }

    private static StringComparison GetPathComparison ()
    {
        return IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
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
