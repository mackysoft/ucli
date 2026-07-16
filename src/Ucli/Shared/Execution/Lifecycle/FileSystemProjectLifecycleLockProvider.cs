using System.Text;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Shared.Execution.Lifecycle;

/// <summary> Implements filesystem-backed lifecycle locks scoped by physical Unity project root. </summary>
internal sealed class FileSystemProjectLifecycleLockProvider : IProjectLifecycleLockProvider
{
    private const string LockFileName = "lifecycle.lock";

    private const int RetryDelayMilliseconds = 50;

    private const int SharingViolationHResult = unchecked((int)0x80070020);

    private const int LockViolationHResult = unchecked((int)0x80070021);

    private const int PosixResourceTemporarilyUnavailableHResult = 11;

    private const int PosixNoLocksAvailableHResult = 35;

    private readonly string lockStorageBoundaryRoot;

    private readonly string lockStorageRoot;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="FileSystemProjectLifecycleLockProvider" /> class. </summary>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <param name="lockStorageRoot"> The optional root used to store lock files. Intended for tests. </param>
    public FileSystemProjectLifecycleLockProvider (
        TimeProvider timeProvider,
        string? lockStorageRoot = null)
    {
        if (string.IsNullOrWhiteSpace(lockStorageRoot))
        {
            var defaultStoragePaths = ResolveDefaultLockStoragePaths();
            this.lockStorageBoundaryRoot = defaultStoragePaths.BoundaryRoot;
            this.lockStorageRoot = defaultStoragePaths.StorageRoot;
        }
        else
        {
            this.lockStorageRoot = NormalizePathArgument(lockStorageRoot, nameof(lockStorageRoot));
            this.lockStorageBoundaryRoot = this.lockStorageRoot;
        }

        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Acquires the lifecycle lock for one physical Unity project root. </summary>
    /// <param name="request"> The lifecycle lock request whose Unity project root scopes the lock. </param>
    /// <param name="timeout"> The timeout budget used while waiting for lock acquisition. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The async-disposable lock handle that must be disposed to release lock. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when the project root is empty or has an invalid path format. </exception>
    /// <exception cref="DirectoryNotFoundException"> Thrown when the project root no longer exists. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="TimeoutException"> Thrown when lock acquisition exceeds <paramref name="timeout" />. </exception>
    public async ValueTask<IAsyncDisposable> AcquireAsync (
        ProjectLifecycleLockRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var lockFilePath = ResolveLockFilePath(request.UnityProjectRoot);
        var lockDirectoryPath = Path.GetDirectoryName(lockFilePath);
        if (!string.IsNullOrWhiteSpace(lockDirectoryPath))
        {
            FileSystemAccessBoundary.EnsureSecureDirectoryChain(lockStorageBoundaryRoot, lockDirectoryPath);
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
            catch (IOException exception) when (IsLockContentionException(exception))
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

                await TimeProviderDelay.DelayAsync(delay, timeProvider, cancellationToken).ConfigureAwait(false);
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
        return PathStringNormalizer.NormalizeAbsolutePathForStableIdentity(resolvedPath);
    }

    private static string ResolveSymbolicLinksUntilStable (string fullPath)
    {
        var currentPath = fullPath;
        for (var i = 0; i < 8; i++)
        {
            var resolvedPath = ResolveSymbolicLinks(currentPath);
            if (PathIdentity.IsSamePath(resolvedPath, currentPath))
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
            var candidatePath = ResolveExistingPathSegment(currentPath, pathSegments[i]);
            currentPath = ResolveSymbolicLinkTarget(candidatePath);
        }

        return currentPath;
    }

    private static string ResolveExistingPathSegment (
        string parentPath,
        string pathSegment)
    {
        string? ignoreCaseMatch = null;
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(parentPath))
        {
            var entryName = Path.GetFileName(entryPath);
            if (string.Equals(entryName, pathSegment, StringComparison.Ordinal))
            {
                return entryPath;
            }

            if (ignoreCaseMatch == null && string.Equals(entryName, pathSegment, StringComparison.OrdinalIgnoreCase))
            {
                ignoreCaseMatch = entryPath;
            }
        }

        return ignoreCaseMatch ?? Path.Combine(parentPath, pathSegment);
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

    private static (string BoundaryRoot, string StorageRoot) ResolveDefaultLockStoragePaths ()
    {
        var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationDataPath))
        {
            throw new InvalidOperationException("OS local application data path could not be resolved.");
        }

        var boundaryRoot = Path.Combine(
            NormalizePathArgument(localApplicationDataPath, nameof(localApplicationDataPath)),
            "MackySoft");
        var storageRoot = Path.Combine(
            boundaryRoot,
            "ucli",
            "lifecycle-locks",
            "unity-projects");
        return (boundaryRoot, storageRoot);
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

    private static bool IsLockContentionException (IOException exception)
    {
        return exception.HResult is SharingViolationHResult
            or LockViolationHResult
            or PosixResourceTemporarilyUnavailableHResult
            or PosixNoLocksAvailableHResult;
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
