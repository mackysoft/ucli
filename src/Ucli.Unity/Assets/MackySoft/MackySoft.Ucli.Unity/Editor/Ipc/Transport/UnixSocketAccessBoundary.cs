using System;
using System.IO;
using System.Runtime.InteropServices;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Applies same-user filesystem boundary rules for one unix-domain-socket listener path. </summary>
    internal sealed class UnixSocketAccessBoundary
    {
        private const int OwnerOnlyDirectoryMode = 0x1C0;

        private const int OwnerOnlyFileMode = 0x180;

        private readonly string socketPath;

        /// <summary> Initializes a new instance of the <see cref="UnixSocketAccessBoundary" /> class. </summary>
        /// <param name="socketPath"> The target unix-domain-socket path. </param>
        public UnixSocketAccessBoundary (string socketPath)
        {
            this.socketPath = socketPath ?? throw new ArgumentNullException(nameof(socketPath));
        }

        /// <summary> Ensures the socket directory is secure and removes stale socket residue before bind. </summary>
        public void PrepareForBind ()
        {
            var socketDirectoryPath = Path.GetDirectoryName(socketPath);
            if (!string.IsNullOrWhiteSpace(socketDirectoryPath))
            {
                UcliLocalStorageBootstrapper.EnsureInitialized(socketDirectoryPath);
                EnsureSecureDirectory(socketDirectoryPath);
            }

            FileUtilities.DeleteIfExists(socketPath);
        }

        /// <summary> Applies owner-only mode to the bound socket node. </summary>
        public void HardenBoundSocket ()
        {
            ApplyOwnerOnlyFileMode(socketPath);
        }

        /// <summary> Removes stale socket residue and cleans up empty fallback directory when applicable. </summary>
        public void Cleanup ()
        {
            FileUtilities.DeleteIfExists(socketPath);
            DeleteEmptyFallbackDirectoryIfPresent(socketPath);
        }

        private static void EnsureSecureDirectory (string directoryPath)
        {
            var normalizedDirectoryPath = Path.GetFullPath(directoryPath);
            if (TryResolveLocalDirectoryRoot(normalizedDirectoryPath, out var localDirectoryRoot))
            {
                var pendingDirectories = new System.Collections.Generic.Stack<string>();
                var currentDirectoryPath = normalizedDirectoryPath;
                while (true)
                {
                    pendingDirectories.Push(currentDirectoryPath);
                    if (string.Equals(currentDirectoryPath, localDirectoryRoot, StringComparison.Ordinal))
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
                    ApplyOwnerOnlyDirectoryMode(currentPath);
                }

                return;
            }

            Directory.CreateDirectory(normalizedDirectoryPath);
            ApplyOwnerOnlyDirectoryMode(normalizedDirectoryPath);
        }

        private static bool TryResolveLocalDirectoryRoot (
            string directoryPath,
            out string localDirectoryRoot)
        {
            var currentDirectory = new DirectoryInfo(directoryPath);
            while (currentDirectory != null)
            {
                var parentDirectory = currentDirectory.Parent;
                if (string.Equals(currentDirectory.Name, UcliStoragePathNames.LocalDirectoryName, StringComparison.Ordinal)
                    && parentDirectory != null
                    && string.Equals(parentDirectory.Name, UcliStoragePathNames.UcliDirectoryName, StringComparison.Ordinal))
                {
                    localDirectoryRoot = currentDirectory.FullName;
                    return true;
                }

                currentDirectory = parentDirectory;
            }

            localDirectoryRoot = string.Empty;
            return false;
        }

        private static void DeleteEmptyFallbackDirectoryIfPresent (string socketPath)
        {
            if (!string.Equals(Path.GetFileName(socketPath), UcliIpcEndpointNames.UnixSocketFileName, StringComparison.Ordinal))
            {
                return;
            }

            var normalizedSocketPath = Path.GetFullPath(socketPath);
            var directoryPath = Path.GetDirectoryName(normalizedSocketPath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }

            var normalizedDirectoryPath = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var tempRootPath = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parentDirectoryPath = Path.GetDirectoryName(normalizedDirectoryPath);
            if (!string.Equals(parentDirectoryPath, tempRootPath, StringComparison.Ordinal))
            {
                return;
            }

            var directoryName = Path.GetFileName(normalizedDirectoryPath);
            if (!directoryName.StartsWith(UcliIpcEndpointNames.DaemonAddressPrefix, StringComparison.Ordinal))
            {
                return;
            }

            if (Directory.Exists(normalizedDirectoryPath) && IsEmptyDirectory(normalizedDirectoryPath))
            {
                Directory.Delete(normalizedDirectoryPath);
            }
        }

        private static bool IsEmptyDirectory (string directoryPath)
        {
            using var enumerator = Directory.EnumerateFileSystemEntries(directoryPath).GetEnumerator();
            return !enumerator.MoveNext();
        }

        private static void ApplyOwnerOnlyDirectoryMode (string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            if (Chmod(path, OwnerOnlyDirectoryMode) != 0)
            {
                throw new IOException($"chmod failed for directory '{path}'. errno={Marshal.GetLastWin32Error()}");
            }
        }

        private static void ApplyOwnerOnlyFileMode (string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            if (Chmod(path, OwnerOnlyFileMode) != 0)
            {
                throw new IOException($"chmod failed for socket '{path}'. errno={Marshal.GetLastWin32Error()}");
            }
        }

        [DllImport("libc", SetLastError = true, EntryPoint = "chmod")]
        private static extern int Chmod (string pathname, int mode);
    }
}
