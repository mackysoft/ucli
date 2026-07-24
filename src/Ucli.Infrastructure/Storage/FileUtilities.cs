using System.Buffers;
using System.Text;
using MackySoft.FileSystem;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Provides shared utility operations for filesystem files. </summary>
public static class FileUtilities
{
    internal const string AtomicWriteTemporaryFileNamePrefix = ".tmp-";

    private const int FileReadBufferSize = 4096;

    private const int TemporaryFileCreationAttemptLimit = 10;

    private const int FileReplacementRetryLimit = 20;

    private const int FileReplacementRetryDelayMilliseconds = 5;

    private const int WindowsSharingViolationHResult = unchecked((int)0x80070020);

    private const int WindowsUnableToRemoveReplacedHResult = unchecked((int)0x80070497);

    /// <summary> Reads one guarded file as text without blocking concurrent atomic replacement. </summary>
    internal static string? ReadAllTextOrNull (AbsolutePath path)
    {
        try
        {
            using var stream = OpenReopenSafeReadStream(path);
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                FileReadBufferSize,
                leaveOpen: false);
            return reader.ReadToEnd();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary> Reads one guarded file as text, or returns <see langword="null" /> when it does not exist. </summary>
    internal static async ValueTask<string?> ReadAllTextOrNullAsync (
        AbsolutePath path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var stream = OpenReopenSafeReadStream(path);
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                FileReadBufferSize,
                leaveOpen: false);
            var buffer = ArrayPool<char>.Shared.Rent(FileReadBufferSize);
            try
            {
                var contents = new StringBuilder();
                while (true)
                {
                    var readCount = await reader.ReadAsync(
                            buffer.AsMemory(0, FileReadBufferSize),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (readCount == 0)
                    {
                        return contents.ToString();
                    }

                    contents.Append(buffer, 0, readCount);
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary> Reads the exact bytes of one guarded file without blocking concurrent atomic replacement. </summary>
    internal static ValueTask<ReadOnlyMemory<byte>?> ReadAllBytesOrNullAsync (
        AbsolutePath path,
        CancellationToken cancellationToken)
    {
        return ReadBytesOrNullCoreAsync(path, maximumBytes: null, cancellationToken);
    }

    /// <summary> Reads at most the specified number of bytes from one guarded file. </summary>
    internal static ValueTask<ReadOnlyMemory<byte>?> ReadBytesOrNullWithinLimitAsync (
        AbsolutePath path,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBytes),
                maximumBytes,
                "Maximum byte count must be greater than zero.");
        }

        return ReadBytesOrNullCoreAsync(path, maximumBytes, cancellationToken);
    }

    private static async ValueTask<ReadOnlyMemory<byte>?> ReadBytesOrNullCoreAsync (
        AbsolutePath path,
        int? maximumBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var stream = OpenReopenSafeReadStream(path);
            if (maximumBytes.HasValue && stream.Length > maximumBytes.Value)
            {
                throw new IOException(
                    $"File exceeds the maximum size of {maximumBytes.Value} bytes: {path.Value}");
            }

            using var contents = new MemoryStream();
            var buffer = ArrayPool<byte>.Shared.Rent(FileReadBufferSize);
            try
            {
                long totalBytesRead = 0;
                while (true)
                {
                    var readCount = await stream.ReadAsync(
                            buffer.AsMemory(0, FileReadBufferSize),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (readCount == 0)
                    {
                        return new ReadOnlyMemory<byte>(contents.ToArray());
                    }

                    totalBytesRead += readCount;
                    if (maximumBytes.HasValue && totalBytesRead > maximumBytes.Value)
                    {
                        throw new IOException(
                            $"File exceeds the maximum size of {maximumBytes.Value} bytes: {path.Value}");
                    }

                    contents.Write(buffer, 0, readCount);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary> Opens a read handle for a guarded path without blocking concurrent atomic replacement. </summary>
    internal static FileStream OpenReopenSafeReadStream (AbsolutePath path)
    {
        EnsureRegularFile(path, "Read source");

        return new FileStream(
            path.Value,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            FileReadBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    /// <summary> Writes text atomically to one guarded target file path. </summary>
    internal static async ValueTask WriteAllTextAtomicallyAsync (
        AbsolutePath path,
        string contents,
        CancellationToken cancellationToken = default)
    {
        if (contents == null)
        {
            throw new ArgumentNullException(nameof(contents));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!path.TryGetParent(out var directoryPath))
        {
            throw new InvalidOperationException($"Directory path could not be resolved: {path.Value}");
        }
        Directory.CreateDirectory(directoryPath.Value);
        var temporaryStream = OpenAtomicWriteTemporaryFileInDirectory(directoryPath, out var temporaryPath);
        var temporaryFileOwned = true;

        try
        {
            using (temporaryStream)
            using (var writer = new StreamWriter(
                       temporaryStream,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       FileReadBufferSize,
                       leaveOpen: false))
            {
                await writer.WriteAsync(contents.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            await PublishAtomicWriteTemporaryFileAsync(temporaryPath, path, cancellationToken).ConfigureAwait(false);
            temporaryFileOwned = false;
        }
        finally
        {
            if (temporaryFileOwned)
            {
                DeleteIfExists(temporaryPath);
            }
        }
    }

    /// <summary> Writes exact bytes atomically to the target file path. </summary>
    /// <param name="path"> The target file path. </param>
    /// <param name="contents"> The borrowed byte contents retained by the caller until this operation completes. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when the write operation finishes. </returns>
    internal static async ValueTask WriteAllBytesAtomicallyAsync (
        AbsolutePath path,
        ReadOnlyMemory<byte> contents,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!path.TryGetParent(out var directoryPath))
        {
            throw new InvalidOperationException($"Directory path could not be resolved: {path.Value}");
        }
        Directory.CreateDirectory(directoryPath.Value);
        var temporaryStream = OpenAtomicWriteTemporaryFileInDirectory(directoryPath, out var temporaryPath);
        var temporaryFileOwned = true;

        try
        {
            using (temporaryStream)
            {
                await temporaryStream.WriteAsync(contents, cancellationToken).ConfigureAwait(false);
                await temporaryStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            await PublishAtomicWriteTemporaryFileAsync(temporaryPath, path, cancellationToken).ConfigureAwait(false);
            temporaryFileOwned = false;
        }
        finally
        {
            if (temporaryFileOwned)
            {
                DeleteIfExists(temporaryPath);
            }
        }
    }

    /// <summary> Writes text atomically to one guarded target file path. </summary>
    internal static void WriteAllTextAtomically (
        AbsolutePath path,
        string contents)
    {
        if (contents == null)
        {
            throw new ArgumentNullException(nameof(contents));
        }

        if (!path.TryGetParent(out var directoryPath))
        {
            throw new InvalidOperationException($"Directory path could not be resolved: {path.Value}");
        }
        Directory.CreateDirectory(directoryPath.Value);
        var temporaryStream = OpenAtomicWriteTemporaryFileInDirectory(directoryPath, out var temporaryPath);
        var temporaryFileOwned = true;

        try
        {
            using (temporaryStream)
            using (var writer = new StreamWriter(
                       temporaryStream,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       FileReadBufferSize,
                       leaveOpen: false))
            {
                writer.Write(contents);
            }

            PublishAtomicWriteTemporaryFile(temporaryPath, path);
            temporaryFileOwned = false;
        }
        finally
        {
            if (temporaryFileOwned)
            {
                DeleteIfExists(temporaryPath);
            }
        }
    }

    /// <summary> Deletes one guarded file and treats a missing file as a valid no-op state. </summary>
    internal static void DeleteIfExists (AbsolutePath path)
    {
        if (File.Exists(path.Value))
        {
            File.Delete(path.Value);
        }
    }

    /// <summary>
    /// Atomically publishes a temporary file created by
    /// <see cref="OpenAtomicWriteTemporaryFileInDirectory" /> and retries transient Windows replacement failures.
    /// </summary>
    /// <param name="temporaryPath"> The owned temporary file path consumed on success. </param>
    /// <param name="path"> The destination path in the same directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated while waiting between replacement attempts. </param>
    /// <returns> A task that completes after the destination owns the temporary file contents. </returns>
    internal static async ValueTask PublishAtomicWriteTemporaryFileAsync (
        AbsolutePath temporaryPath,
        AbsolutePath path,
        CancellationToken cancellationToken)
    {
        ValidateAtomicWritePublication(temporaryPath, path);

        var failureCount = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ReplaceFileOnce(temporaryPath, path);
                return;
            }
            catch (IOException exception)
            {
                failureCount++;
                var retryDelay = ResolveFileReplacementRetryDelay(exception, failureCount);
                if (retryDelay is null)
                {
                    throw;
                }

                await Task.Delay(retryDelay.Value, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Atomically publishes a temporary file created by
    /// <see cref="OpenAtomicWriteTemporaryFileInDirectory" /> and retries transient Windows replacement failures.
    /// </summary>
    /// <param name="temporaryPath"> The owned temporary file path consumed on success. </param>
    /// <param name="path"> The destination path in the same directory. </param>
    internal static void PublishAtomicWriteTemporaryFile (
        AbsolutePath temporaryPath,
        AbsolutePath path)
    {
        ValidateAtomicWritePublication(temporaryPath, path);

        var failureCount = 0;
        while (true)
        {
            try
            {
                ReplaceFileOnce(temporaryPath, path);
                return;
            }
            catch (IOException exception)
            {
                failureCount++;
                var retryDelay = ResolveFileReplacementRetryDelay(exception, failureCount);
                if (retryDelay is null)
                {
                    throw;
                }

                Thread.Sleep(retryDelay.Value);
            }
        }
    }

    /// <summary> Resolves the bounded retry delay for one failed atomic replacement attempt. </summary>
    /// <param name="exception"> The I/O failure raised by the replacement operation. </param>
    /// <param name="failureCount"> The one-based count of consecutive replacement failures. </param>
    /// <returns> The retry delay for a recoverable Windows replacement failure within the retry limit; otherwise <see langword="null" />. </returns>
    internal static TimeSpan? ResolveFileReplacementRetryDelay (
        IOException exception,
        int failureCount)
    {
        if (exception == null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        if (failureCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failureCount), failureCount, "failureCount must be greater than zero.");
        }

        if (exception.HResult is not WindowsSharingViolationHResult
                and not WindowsUnableToRemoveReplacedHResult
            || failureCount > FileReplacementRetryLimit)
        {
            return null;
        }

        return TimeSpan.FromMilliseconds(FileReplacementRetryDelayMilliseconds * failureCount);
    }

    /// <summary> Creates and exclusively opens a short-named temporary file in a guarded publication directory. </summary>
    internal static FileStream OpenAtomicWriteTemporaryFileInDirectory (
        AbsolutePath directoryPath,
        out AbsolutePath temporaryPath)
    {
        if (directoryPath is null)
        {
            throw new ArgumentNullException(nameof(directoryPath));
        }
        temporaryPath = null!;
        for (var attempt = 0; attempt < TemporaryFileCreationAttemptLimit; attempt++)
        {
            var candidatePath = ContainedPath.Create(
                directoryPath,
                RootRelativePath.Parse(CreateAtomicWriteTemporaryFileName())).Target;
            try
            {
                var stream = new FileStream(
                    candidatePath.Value,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    FileReadBufferSize,
                    FileOptions.Asynchronous);
                temporaryPath = candidatePath;
                return stream;
            }
            catch (IOException) when (File.Exists(candidatePath.Value) || Directory.Exists(candidatePath.Value))
            {
                // A concurrent reservation owns this random name; retry with another name.
            }
        }

        throw new IOException(
            $"Could not reserve a temporary file after {TemporaryFileCreationAttemptLimit} attempts: {directoryPath.Value}");
    }

    /// <summary> Creates a short random file name used by atomic writers before publication. </summary>
    /// <returns> A file name beginning with <c>.tmp-</c>. </returns>
    internal static string CreateAtomicWriteTemporaryFileName ()
    {
        return AtomicWriteTemporaryFileNamePrefix + Path.GetRandomFileName();
    }

    private static void ValidateAtomicWritePublication (
        AbsolutePath temporaryPath,
        AbsolutePath path)
    {
        if (temporaryPath is null)
        {
            throw new ArgumentNullException(nameof(temporaryPath));
        }

        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }
        if (temporaryPath.IsSameAs(path))
        {
            throw new ArgumentException(
                "Atomic write temporary file and destination must be different paths.",
                nameof(path));
        }

        if (!temporaryPath.TryGetParent(out var temporaryDirectoryPath))
        {
            throw new InvalidOperationException(
                $"Temporary file directory path could not be resolved: {temporaryPath.Value}");
        }

        if (!path.TryGetParent(out var destinationDirectoryPath))
        {
            throw new InvalidOperationException(
                $"Destination directory path could not be resolved: {path.Value}");
        }
        if (!temporaryDirectoryPath.IsSameAs(destinationDirectoryPath))
        {
            throw new ArgumentException(
                "Atomic write temporary file and destination must share one directory.",
                nameof(temporaryPath));
        }

        if (!Path.GetFileName(temporaryPath.Value).StartsWith(
                AtomicWriteTemporaryFileNamePrefix,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Atomic write temporary file name is not owned by FileUtilities.",
                nameof(temporaryPath));
        }

        EnsureRegularFile(temporaryPath, "Atomic write temporary file");
        EnsureWritableAtomicDestination(path);
    }

    private static void ReplaceFileOnce (
        AbsolutePath temporaryPath,
        AbsolutePath path)
    {
        EnsureWritableAtomicDestination(path);
        try
        {
            File.Replace(temporaryPath.Value, path.Value, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (FileNotFoundException)
        {
            MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
        }
        catch (IOException) when (!File.Exists(path.Value))
        {
            MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
        }
    }

    private static void MoveOrReplaceWhenCreatedConcurrently (
        AbsolutePath temporaryPath,
        AbsolutePath path)
    {
        try
        {
            File.Move(temporaryPath.Value, path.Value);
        }
        catch (IOException) when (File.Exists(path.Value))
        {
            EnsureWritableAtomicDestination(path);
            File.Replace(temporaryPath.Value, path.Value, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
    }

    private static void EnsureWritableAtomicDestination (AbsolutePath path)
    {
        try
        {
            EnsureRegularFile(path, "Atomic write destination");
        }
        catch (FileNotFoundException)
        {
            return;
        }
    }

    /// <summary> Ensures an existing guarded path is a regular file and not a reparse point. </summary>
    internal static void EnsureRegularFile (
        AbsolutePath path,
        string subject)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path.Value);
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException($"{subject} was not found: {path.Value}", path.Value);
        }
        catch (DirectoryNotFoundException)
        {
            throw new FileNotFoundException($"{subject} was not found: {path.Value}", path.Value);
        }
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"{subject} must not be a reparse point: {path.Value}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"{subject} must not be a directory: {path.Value}");
        }

        if (!FileSystemNodeClassifier.IsRegularFile(path, attributes))
        {
            throw new IOException($"{subject} must be a regular file: {path.Value}");
        }
    }
}
