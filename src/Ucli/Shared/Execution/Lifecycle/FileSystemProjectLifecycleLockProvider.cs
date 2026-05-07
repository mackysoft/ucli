using System.Text;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Infrastructure.Cryptography;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Shared.Execution.Lifecycle;

/// <summary> Implements filesystem-backed lifecycle locks scoped by physical Unity project root. </summary>
internal sealed class FileSystemProjectLifecycleLockProvider : IProjectLifecycleLockProvider
{
    private const string LockFileName = "lifecycle.lock";

    private const int RetryDelayMilliseconds = 50;

    private readonly string lockStorageRoot;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="FileSystemProjectLifecycleLockProvider" /> class. </summary>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <param name="lockStorageRoot"> The optional root used to store lock files. Intended for tests. </param>
    public FileSystemProjectLifecycleLockProvider (
        TimeProvider? timeProvider = null,
        string? lockStorageRoot = null)
    {
        this.lockStorageRoot = string.IsNullOrWhiteSpace(lockStorageRoot)
            ? ResolveDefaultLockStorageRoot()
            : NormalizePathArgument(lockStorageRoot, nameof(lockStorageRoot));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Acquires the lifecycle lock for one resolved Unity project root. </summary>
    /// <param name="unityProject"> The resolved Unity project context whose physical project root scopes the lock. </param>
    /// <param name="timeout"> The timeout budget used while waiting for lock acquisition. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The async-disposable lock handle that must be disposed to release lock. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the project root is empty or has an invalid path format. </exception>
    /// <exception cref="DirectoryNotFoundException"> Thrown when the project root no longer exists. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="TimeoutException"> Thrown when lock acquisition exceeds <paramref name="timeout" />. </exception>
    public async ValueTask<IAsyncDisposable> Acquire (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var lockFilePath = ResolveLockFilePath(unityProject.UnityProjectRoot);
        var lockDirectoryPath = Path.GetDirectoryName(lockFilePath);
        if (!string.IsNullOrWhiteSpace(lockDirectoryPath))
        {
            FileSystemAccessBoundary.EnsureSecureDirectory(lockDirectoryPath);
        }

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var stream = new FileStream(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
                return new LockHandle(stream);
            }
            catch (IOException)
            {
                if (!deadline.TryGetRemainingTimeout(out var remaining))
                {
                    throw new TimeoutException(
                        $"Timed out while waiting to acquire project lifecycle lock. Timeout={timeout.TotalMilliseconds:0}ms.");
                }

                var retryDelay = TimeSpan.FromMilliseconds(RetryDelayMilliseconds);
                var delay = remaining < retryDelay
                    ? remaining
                    : retryDelay;
                if (delay <= TimeSpan.Zero)
                {
                    delay = TimeSpan.FromMilliseconds(1);
                }

                await TimeProviderDelay.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private string ResolveLockFilePath (string unityProjectRoot)
    {
        var physicalProjectRoot = ResolvePhysicalProjectRoot(unityProjectRoot);
        var lockKey = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(physicalProjectRoot));
        return Path.Combine(lockStorageRoot, lockKey, LockFileName);
    }

    private static string ResolvePhysicalProjectRoot (string unityProjectRoot)
    {
        if (string.IsNullOrWhiteSpace(unityProjectRoot))
        {
            throw new ArgumentException("Unity project root must not be empty.", nameof(unityProjectRoot));
        }

        var normalizedProjectRoot = NormalizePathArgument(unityProjectRoot, nameof(unityProjectRoot));
        if (!Directory.Exists(normalizedProjectRoot))
        {
            throw new DirectoryNotFoundException($"Unity project root was not found: {normalizedProjectRoot}");
        }

        var resolvedPath = ResolveSymbolicLinksUntilStable(normalizedProjectRoot);
        return NormalizePhysicalPathForKey(resolvedPath);
    }

    private static string ResolveSymbolicLinksUntilStable (string fullPath)
    {
        var currentPath = fullPath;
        for (var i = 0; i < 8; i++)
        {
            var resolvedPath = ResolveSymbolicLinks(currentPath);
            if (string.Equals(resolvedPath, currentPath, GetPathComparison()))
            {
                return resolvedPath;
            }

            currentPath = resolvedPath;
        }

        return currentPath;
    }

    private static string ResolveSymbolicLinks (string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return fullPath;
        }

        var relativePath = fullPath[root.Length..];
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return root;
        }

        var pathSegments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        var currentPath = root;

        for (var i = 0; i < pathSegments.Length; i++)
        {
            var candidatePath = Path.Combine(currentPath, pathSegments[i]);
            currentPath = ResolveSymbolicLinkTarget(candidatePath);
        }

        return currentPath;
    }

    private static string ResolveSymbolicLinkTarget (string path)
    {
        FileSystemInfo fileSystemInfo;
        if (Directory.Exists(path))
        {
            fileSystemInfo = new DirectoryInfo(path);
        }
        else if (File.Exists(path))
        {
            fileSystemInfo = new FileInfo(path);
        }
        else
        {
            return path;
        }

        if (string.IsNullOrEmpty(fileSystemInfo.LinkTarget))
        {
            return path;
        }

        var finalTarget = fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true);
        return finalTarget == null
            ? path
            : Path.GetFullPath(finalTarget.FullName);
    }

    private static string NormalizePhysicalPathForKey (string path)
    {
        var normalizedPath = NormalizePathArgument(path, nameof(path));
        normalizedPath = PathStringNormalizer.ReplaceAltSeparatorWithPlatformSeparator(normalizedPath);
        var pathRoot = Path.GetPathRoot(normalizedPath);
        if (!string.IsNullOrEmpty(pathRoot) && string.Equals(normalizedPath, pathRoot, GetPathComparison()))
        {
            return PathStringNormalizer.NormalizeCaseForCurrentPlatform(normalizedPath);
        }

        var trimmedPath = PathStringNormalizer.TrimTrailingDirectorySeparators(normalizedPath);
        return PathStringNormalizer.NormalizeCaseForCurrentPlatform(trimmedPath);
    }

    private static string ResolveDefaultLockStorageRoot ()
    {
        var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationDataPath))
        {
            throw new InvalidOperationException("OS local application data path could not be resolved.");
        }

        return Path.Combine(
            NormalizePathArgument(localApplicationDataPath, nameof(localApplicationDataPath)),
            "MackySoft",
            "ucli",
            "lifecycle-locks",
            "unity-projects");
    }

    private static string NormalizePathArgument (
        string pathValue,
        string parameterName)
    {
        var pathResult = PathNormalizer.TryNormalizeFullPath(pathValue);
        if (!pathResult.IsSuccess)
        {
            throw new ArgumentException(pathResult.DiagnosticMessage, parameterName);
        }

        return pathResult.FullPath!;
    }

    private static StringComparison GetPathComparison ()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    /// <summary> Releases one file lock handle when disposed. </summary>
    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly FileStream lockStream;

        private bool disposed;

        /// <summary> Initializes a new instance of the <see cref="LockHandle" /> class. </summary>
        /// <param name="lockStream"> The lock file stream to release on dispose. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="lockStream" /> is <see langword="null" />. </exception>
        public LockHandle (FileStream lockStream)
        {
            this.lockStream = lockStream ?? throw new ArgumentNullException(nameof(lockStream));
        }

        /// <summary> Releases the held file lock once. </summary>
        /// <returns> A completed task. </returns>
        public ValueTask DisposeAsync ()
        {
            if (disposed)
            {
                return ValueTask.CompletedTask;
            }

            disposed = true;
            lockStream.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
