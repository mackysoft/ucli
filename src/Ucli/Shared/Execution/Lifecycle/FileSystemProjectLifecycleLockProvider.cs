using System.Text;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Contracts.Cryptography;
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

    private readonly AbsolutePath lockStorageBoundaryRoot;

    private readonly AbsolutePath lockStorageRoot;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="FileSystemProjectLifecycleLockProvider" /> class. </summary>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <param name="lockStorageRoot"> The optional root used to store lock files. Intended for tests. </param>
    public FileSystemProjectLifecycleLockProvider (
        TimeProvider timeProvider,
        AbsolutePath? lockStorageRoot = null)
    {
        if (lockStorageRoot is null)
        {
            var defaultStoragePaths = ResolveDefaultLockStoragePaths();
            this.lockStorageBoundaryRoot = defaultStoragePaths.BoundaryRoot;
            this.lockStorageRoot = defaultStoragePaths.StorageRoot;
        }
        else
        {
            this.lockStorageRoot = lockStorageRoot;
            this.lockStorageBoundaryRoot = lockStorageRoot;
        }

        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Acquires the lifecycle lock for one physical Unity project root. </summary>
    /// <param name="request"> The lifecycle lock request whose Unity project root scopes the lock. </param>
    /// <param name="timeout"> The timeout budget used while waiting for lock acquisition. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The async-disposable lock handle that must be disposed to release lock. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
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
        if (lockFilePath.TryGetParent(out var lockDirectoryPath))
        {
            FileSystemAccessBoundary.EnsureSecureDirectoryChain(
                ContainedPath.Create(lockStorageBoundaryRoot, lockDirectoryPath));
        }

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var stream = new FileStream(
                    lockFilePath.Value,
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

    private AbsolutePath ResolveLockFilePath (AbsolutePath unityProjectRoot)
    {
        var physicalProjectRoot = ResolvePhysicalProjectRoot(unityProjectRoot);
        var identityText = CreateLockIdentityText(physicalProjectRoot);
        var lockKey = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(identityText));
        return ContainedPath.Create(
            lockStorageRoot,
            RootRelativePath.Parse($"{lockKey}/{LockFileName}")).Target;
    }

    private static AbsolutePath ResolvePhysicalProjectRoot (AbsolutePath unityProjectRoot)
    {
        var requestedPath = ContainedPath.Create(unityProjectRoot.GetRoot(), unityProjectRoot);
        if (!PhysicalPathResolver.TryResolve(
                requestedPath,
                SymbolicLinkHandling.Follow,
                MissingPathHandling.Reject,
                out var resolution,
                out var failure))
        {
            throw CreateResolutionException(failure);
        }

        return ResolveActualCasing(resolution.ResolvedPath.Target);
    }

    private static Exception CreateResolutionException (FileSystemOperationFailure failure)
    {
        var message = $"Physical Unity project root resolution failed. Kind={failure.Kind}; Path={failure.Path}; Diagnostic={failure.Message}";
        return failure.Kind switch
        {
            FileSystemOperationFailureKind.EntryNotFound => new DirectoryNotFoundException(message),
            FileSystemOperationFailureKind.AccessDenied => new UnauthorizedAccessException(message),
            FileSystemOperationFailureKind.PlatformNotSupported => new PlatformNotSupportedException(message),
            FileSystemOperationFailureKind.LinkCycle
                or FileSystemOperationFailureKind.UnexpectedEntryKind
                or FileSystemOperationFailureKind.IoFailure
                or FileSystemOperationFailureKind.OutsideBoundary
                or FileSystemOperationFailureKind.ConcurrentChange => new IOException(message),
            _ => new InvalidOperationException(message),
        };
    }

    private static AbsolutePath ResolveActualCasing (AbsolutePath resolvedPath)
    {
        var root = resolvedPath.GetRoot();
        if (root.IsSameAs(resolvedPath))
        {
            return root;
        }

        var pathSegments = new Stack<RootRelativePath>();
        var lexicalPath = resolvedPath;
        while (lexicalPath.TryGetParent(out var lexicalParent))
        {
            pathSegments.Push(
                ContainedPath.Create(lexicalParent, lexicalPath).RelativePath);
            lexicalPath = lexicalParent;
        }

        var currentPath = root;
        while (pathSegments.TryPop(out var pathSegment))
        {
            currentPath = ResolveExistingPathSegment(currentPath, pathSegment);
        }

        return currentPath;
    }

    private static AbsolutePath ResolveExistingPathSegment (
        AbsolutePath parentPath,
        RootRelativePath pathSegment)
    {
        AbsolutePath? ignoreCaseMatch = null;
        foreach (var entryPathValue in Directory.EnumerateFileSystemEntries(parentPath.Value))
        {
            var entryPath = AbsolutePath.Parse(entryPathValue);
            var entryName = ContainedPath.Create(parentPath, entryPath).RelativePath;
            if (string.Equals(entryName.Value, pathSegment.Value, StringComparison.Ordinal))
            {
                return entryPath;
            }

            if (ignoreCaseMatch == null
                && string.Equals(
                    entryName.Value,
                    pathSegment.Value,
                    StringComparison.OrdinalIgnoreCase))
            {
                ignoreCaseMatch = entryPath;
            }
        }

        return ignoreCaseMatch ?? throw new IOException(
            $"A resolved Unity project path segment changed while its actual casing was being read. Parent={parentPath}; Segment={pathSegment}.");
    }

    private static string CreateLockIdentityText (AbsolutePath actualCasedProjectRoot)
    {
        if (!OperatingSystem.IsWindows())
        {
            return actualCasedProjectRoot.Value;
        }

        var root = actualCasedProjectRoot.GetRoot();
        return string.Concat(
            root.Value.ToUpperInvariant(),
            actualCasedProjectRoot.Value[root.Value.Length..]);
    }

    private static (AbsolutePath BoundaryRoot, AbsolutePath StorageRoot) ResolveDefaultLockStoragePaths ()
    {
        var localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!AbsolutePath.TryParse(localApplicationDataPath, out var localApplicationDataRoot, out _))
        {
            throw new InvalidOperationException("OS local application data path could not be resolved.");
        }

        var boundaryRoot = ContainedPath.Create(
            localApplicationDataRoot,
            RootRelativePath.Parse("MackySoft")).Target;
        var storageRoot = ContainedPath.Create(
            boundaryRoot,
            RootRelativePath.Parse("ucli/lifecycle-locks/unity-projects")).Target;
        return (boundaryRoot, storageRoot);
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
