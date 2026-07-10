using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MackySoft.Ucli.Infrastructure.Paths;
using Microsoft.Win32.SafeHandles;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Owns an exclusive filesystem lock that coordinates cooperating processes. </summary>
public sealed class FileExclusiveLock : IDisposable
{
    private const int RetryDelayMilliseconds = 25;

    private const int WindowsSharingViolationHResult = unchecked((int)0x80070020);

    private const int WindowsLockViolationHResult = unchecked((int)0x80070021);

    private const int PosixResourceTemporarilyUnavailableHResult = 11;

    private const int PosixNoLocksAvailableHResult = 35;

    private const int PosixAccessDeniedError = 13;

    private const int NativeUnlockOperation = 0;

    private const int NativeTryLockOperation = 2;

    private const int MacOpenReadWrite = 0x0002;

    private const int MacOpenCloseOnExec = 0x01000000;

    // NOTE:
    // Mono on macOS does not enforce FileShare.None across processes, while modern .NET on macOS
    // does not support FileStream.Lock. Native lockf uses the same advisory byte-range lock as Mono.
    private static readonly bool UseNativeMacRegionLock = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        && Type.GetType("Mono.Runtime") == null;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProcessLocks = new(
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

    private FileStream? lockStream;

    private readonly bool usesNativeRegionLock;

    private readonly SemaphoreSlim processLock;

    private FileExclusiveLock (
        FileStream lockStream,
        bool usesNativeRegionLock,
        SemaphoreSlim processLock)
    {
        this.lockStream = lockStream ?? throw new ArgumentNullException(nameof(lockStream));
        this.usesNativeRegionLock = usesNativeRegionLock;
        this.processLock = processLock ?? throw new ArgumentNullException(nameof(processLock));
    }

    /// <summary> Acquires one exclusive file lock with a bounded wait. </summary>
    /// <param name="lockPath"> The stable lock file path shared by all cooperating processes. </param>
    /// <param name="timeout"> The maximum time to wait for ownership. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The acquired lock, which must be disposed to release ownership. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="lockPath" /> is invalid. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is not positive. </exception>
    /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled before ownership is acquired. </exception>
    /// <exception cref="TimeoutException"> Thrown when another process retains the lock beyond the bounded wait. </exception>
    public static FileExclusiveLock Acquire (
        string lockPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var normalizedLockPath = PrepareLockPath(lockPath, timeout, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var processLock = ProcessLocks.GetOrAdd(normalizedLockPath, static _ => new SemaphoreSlim(1, 1));
        if (!processLock.Wait(timeout, cancellationToken))
        {
            throw CreateTimeoutException(normalizedLockPath, timeout);
        }

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return OpenLock(normalizedLockPath, processLock);
                }
                catch (IOException exception) when (IsLockContention(exception))
                {
                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                    {
                        throw CreateTimeoutException(normalizedLockPath, timeout);
                    }

                    Thread.Sleep(GetRetryDelay(remaining));
                }
            }
        }
        catch
        {
            processLock.Release();
            throw;
        }
    }

    /// <summary> Acquires one exclusive file lock asynchronously with a bounded wait. </summary>
    /// <param name="lockPath"> The stable lock file path shared by all cooperating processes. </param>
    /// <param name="timeout"> The maximum time to wait for ownership. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The acquired lock, which must be disposed to release ownership. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="lockPath" /> is invalid. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is not positive. </exception>
    /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled before ownership is acquired. </exception>
    /// <exception cref="TimeoutException"> Thrown when another process retains the lock beyond the bounded wait. </exception>
    public static async ValueTask<FileExclusiveLock> AcquireAsync (
        string lockPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var normalizedLockPath = PrepareLockPath(lockPath, timeout, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var processLock = ProcessLocks.GetOrAdd(normalizedLockPath, static _ => new SemaphoreSlim(1, 1));
        if (!await processLock.WaitAsync(timeout, cancellationToken).ConfigureAwait(false))
        {
            throw CreateTimeoutException(normalizedLockPath, timeout);
        }

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return OpenLock(normalizedLockPath, processLock);
                }
                catch (IOException exception) when (IsLockContention(exception))
                {
                    var remaining = timeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                    {
                        throw CreateTimeoutException(normalizedLockPath, timeout);
                    }

                    await Task.Delay(GetRetryDelay(remaining), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            processLock.Release();
            throw;
        }
    }

    /// <inheritdoc />
    public void Dispose ()
    {
        var stream = Interlocked.Exchange(ref lockStream, null);
        if (stream == null)
        {
            return;
        }

        try
        {
            if (usesNativeRegionLock)
            {
                ReleaseNativeRegionLock(stream);
            }
            else
            {
#pragma warning disable CA1416 // Unity's Mono runtime supports byte-range locks on macOS.
                stream.Unlock(0, 1);
#pragma warning restore CA1416
            }
        }
        finally
        {
            try
            {
                stream.Dispose();
            }
            finally
            {
                processLock.Release();
            }
        }
    }

    private static string PrepareLockPath (
        string lockPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(lockPath))
        {
            throw new ArgumentException("Lock path must not be empty.", nameof(lockPath));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than zero.");
        }

        var pathResult = PathNormalizer.TryNormalizeFullPath(lockPath);
        if (!pathResult.IsSuccess)
        {
            throw new ArgumentException(pathResult.DiagnosticMessage, nameof(lockPath));
        }

        var directoryPath = Path.GetDirectoryName(pathResult.FullPath!)
            ?? throw new InvalidOperationException($"Lock directory path could not be resolved: {lockPath}");
        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
        return pathResult.FullPath!;
    }

    private static FileExclusiveLock OpenLock (
        string lockPath,
        SemaphoreSlim processLock)
    {
        var stream = UseNativeMacRegionLock
            ? OpenNativeMacStream(lockPath)
            : new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        try
        {
            if (stream.Length == 0)
            {
                stream.SetLength(1);
            }

            if (UseNativeMacRegionLock)
            {
                AcquireNativeRegionLock(stream);
            }
            else
            {
                try
                {
#pragma warning disable CA1416 // Unity's Mono runtime supports byte-range locks on macOS.
                    stream.Lock(0, 1);
#pragma warning restore CA1416
                }
                catch (IOException exception)
                {
                    throw new FileLockContentionException(exception);
                }
            }

            return new FileExclusiveLock(stream, UseNativeMacRegionLock, processLock);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static bool IsLockContention (IOException exception)
    {
        return exception is FileLockContentionException
            || exception.HResult is WindowsSharingViolationHResult
            or WindowsLockViolationHResult
            or PosixResourceTemporarilyUnavailableHResult
            or PosixNoLocksAvailableHResult;
    }

    private static void AcquireNativeRegionLock (FileStream stream)
    {
        stream.Position = 0;
        if (NativeLockf(
                stream.SafeFileHandle.DangerousGetHandle().ToInt32(),
                NativeTryLockOperation,
                size: 1) == 0)
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        var exception = new IOException(
            $"Failed to acquire native exclusive file lock. {new Win32Exception(error).Message}");
        if (error is PosixResourceTemporarilyUnavailableHResult
            or PosixNoLocksAvailableHResult
            or PosixAccessDeniedError)
        {
            throw new FileLockContentionException(exception);
        }

        throw exception;
    }

    private static FileStream OpenNativeMacStream (string lockPath)
    {
        using (new FileStream(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete))
        {
        }

        FileSystemAccessBoundary.EnsureSecureFile(lockPath);
        var fileDescriptor = NativeOpen(lockPath, MacOpenReadWrite | MacOpenCloseOnExec);
        if (fileDescriptor < 0)
        {
            var error = Marshal.GetLastWin32Error();
            throw new IOException(
                $"Failed to open native exclusive lock file. {new Win32Exception(error).Message}");
        }

        var handle = new SafeFileHandle(new IntPtr(fileDescriptor), ownsHandle: true);
        try
        {
            return new FileStream(handle, FileAccess.ReadWrite);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static void ReleaseNativeRegionLock (FileStream stream)
    {
        stream.Position = 0;
        if (NativeLockf(
                stream.SafeFileHandle.DangerousGetHandle().ToInt32(),
                NativeUnlockOperation,
                size: 1) == 0)
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        throw new IOException(
            $"Failed to release native exclusive file lock. {new Win32Exception(error).Message}");
    }

    private static TimeSpan GetRetryDelay (TimeSpan remaining)
    {
        var retryDelay = TimeSpan.FromMilliseconds(RetryDelayMilliseconds);
        return remaining < retryDelay ? remaining : retryDelay;
    }

    private static TimeoutException CreateTimeoutException (
        string lockPath,
        TimeSpan timeout)
    {
        return new TimeoutException(
            $"Timed out while waiting to acquire an exclusive file lock. Timeout={timeout.TotalMilliseconds:0}ms. Path={lockPath}");
    }

    private sealed class FileLockContentionException : IOException
    {
        public FileLockContentionException (IOException innerException)
            : base("The exclusive file lock is owned by another process.", innerException)
        {
        }
    }

    [DllImport("libc", EntryPoint = "lockf", SetLastError = true)]
    private static extern int NativeLockf (
        int fileDescriptor,
        int operation,
        long size);

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int NativeOpen (
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        int flags);
}
