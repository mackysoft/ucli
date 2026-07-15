using System.Buffers;
using System.Text;
using MackySoft.Ucli.Infrastructure.Paths;

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

    /// <summary> Reads one file as text without blocking concurrent atomic replacement. </summary>
    /// <param name="path"> The target file path. </param>
    /// <returns> The text when file exists; otherwise <see langword="null" />. </returns>
    public static string? ReadAllTextOrNull (string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

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

    /// <summary> Reads one file as text, or returns <see langword="null" /> when file does not exist. </summary>
    /// <param name="path"> The target file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The text when file exists; otherwise <see langword="null" />. </returns>
    public static async ValueTask<string?> ReadAllTextOrNullAsync (
        string path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

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

    /// <summary> Reads the exact bytes of one file without blocking concurrent atomic replacement. </summary>
    /// <param name="path"> The target file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Newly owned read-only file bytes when the file exists; otherwise <see langword="null" />. </returns>
    public static ValueTask<ReadOnlyMemory<byte>?> ReadAllBytesOrNullAsync (
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

        return ReadBytesOrNullCoreAsync(path, maximumBytes: null, cancellationToken);
    }

    /// <summary> Reads at most the specified number of exact file bytes without blocking concurrent atomic replacement. </summary>
    /// <param name="path"> The target file path. </param>
    /// <param name="maximumBytes"> The maximum accepted file size in bytes. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> Newly owned read-only file bytes when the file exists; otherwise <see langword="null" />. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="maximumBytes" /> is not positive. </exception>
    /// <exception cref="IOException"> Thrown when the file exceeds <paramref name="maximumBytes" />. </exception>
    public static ValueTask<ReadOnlyMemory<byte>?> ReadBytesOrNullWithinLimitAsync (
        string path,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

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
        string path,
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
                    $"File exceeds the maximum size of {maximumBytes.Value} bytes: {path}");
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
                            $"File exceeds the maximum size of {maximumBytes.Value} bytes: {path}");
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

    /// <summary> Opens a read handle that does not block concurrent atomic file replacement. </summary>
    /// <param name="path"> The target file path. </param>
    /// <returns> The asynchronous sequential-read stream owned by the caller. </returns>
    internal static FileStream OpenReopenSafeReadStream (string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

        EnsureRegularFile(path, "Read source");

        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            FileReadBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    /// <summary> Writes text atomically to the target file path. </summary>
    /// <param name="path"> The target file path. </param>
    /// <param name="contents"> The text contents. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when the write operation finishes. </returns>
    public static async ValueTask WriteAllTextAtomicallyAsync (
        string path,
        string contents,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

        if (contents == null)
        {
            throw new ArgumentNullException(nameof(contents));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var pathResult = PathNormalizer.TryNormalizeFullPath(path);
        if (!pathResult.IsSuccess)
        {
            throw new ArgumentException(pathResult.DiagnosticMessage, nameof(path));
        }

        var directoryPath = Path.GetDirectoryName(pathResult.FullPath!)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {path}");
        Directory.CreateDirectory(directoryPath);
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
    public static async ValueTask WriteAllBytesAtomicallyAsync (
        string path,
        ReadOnlyMemory<byte> contents,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var pathResult = PathNormalizer.TryNormalizeFullPath(path);
        if (!pathResult.IsSuccess)
        {
            throw new ArgumentException(pathResult.DiagnosticMessage, nameof(path));
        }

        var directoryPath = Path.GetDirectoryName(pathResult.FullPath!)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {path}");
        Directory.CreateDirectory(directoryPath);
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

    /// <summary> Writes text atomically to the target file path. </summary>
    /// <param name="path"> The target file path. </param>
    /// <param name="contents"> The text contents. </param>
    public static void WriteAllTextAtomically (
        string path,
        string contents)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

        if (contents == null)
        {
            throw new ArgumentNullException(nameof(contents));
        }

        var pathResult = PathNormalizer.TryNormalizeFullPath(path);
        if (!pathResult.IsSuccess)
        {
            throw new ArgumentException(pathResult.DiagnosticMessage, nameof(path));
        }

        var directoryPath = Path.GetDirectoryName(pathResult.FullPath!)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {path}");
        Directory.CreateDirectory(directoryPath);
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

    /// <summary> Deletes one file and treats a missing file as a valid no-op state. </summary>
    /// <param name="path"> The target file path. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="path" /> is invalid. </exception>
    public static void DeleteIfExists (string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

        if (File.Exists(path))
        {
            File.Delete(path);
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
        string temporaryPath,
        string path,
        CancellationToken cancellationToken)
    {
        ValidateAtomicWritePublication(temporaryPath, path, out temporaryPath, out path);

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
        string temporaryPath,
        string path)
    {
        ValidateAtomicWritePublication(temporaryPath, path, out temporaryPath, out path);

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

    /// <summary> Creates and exclusively opens a short-named temporary file in a validated publication directory. </summary>
    /// <param name="directoryPath"> The existing directory that owns the temporary file. </param>
    /// <param name="temporaryPath"> The reserved temporary file path. </param>
    /// <returns> The exclusive write stream owned by the caller. </returns>
    internal static FileStream OpenAtomicWriteTemporaryFileInDirectory (
        string directoryPath,
        out string temporaryPath)
    {
        temporaryPath = string.Empty;
        for (var attempt = 0; attempt < TemporaryFileCreationAttemptLimit; attempt++)
        {
            var candidatePath = Path.Combine(directoryPath, CreateAtomicWriteTemporaryFileName());
            try
            {
                var stream = new FileStream(
                    candidatePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    FileReadBufferSize,
                    FileOptions.Asynchronous);
                temporaryPath = candidatePath;
                return stream;
            }
            catch (IOException) when (File.Exists(candidatePath) || Directory.Exists(candidatePath))
            {
                // A concurrent reservation owns this random name; retry with another name.
            }
        }

        throw new IOException(
            $"Could not reserve a temporary file after {TemporaryFileCreationAttemptLimit} attempts: {directoryPath}");
    }

    /// <summary> Creates a short random file name used by atomic writers before publication. </summary>
    /// <returns> A file name beginning with <c>.tmp-</c>. </returns>
    internal static string CreateAtomicWriteTemporaryFileName ()
    {
        return AtomicWriteTemporaryFileNamePrefix + Path.GetRandomFileName();
    }

    private static void ValidateAtomicWritePublication (
        string temporaryPath,
        string path,
        out string normalizedTemporaryPath,
        out string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(temporaryPath))
        {
            throw new ArgumentException("Temporary path must not be empty.", nameof(temporaryPath));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Destination path must not be empty.", nameof(path));
        }

        var temporaryPathResult = PathNormalizer.TryNormalizeFullPath(temporaryPath);
        if (!temporaryPathResult.IsSuccess)
        {
            throw new ArgumentException(temporaryPathResult.DiagnosticMessage, nameof(temporaryPath));
        }

        var pathResult = PathNormalizer.TryNormalizeFullPath(path);
        if (!pathResult.IsSuccess)
        {
            throw new ArgumentException(pathResult.DiagnosticMessage, nameof(path));
        }

        normalizedTemporaryPath = temporaryPathResult.FullPath!;
        normalizedPath = pathResult.FullPath!;
        if (PathIdentity.IsSamePath(normalizedTemporaryPath, normalizedPath))
        {
            throw new ArgumentException(
                "Atomic write temporary file and destination must be different paths.",
                nameof(path));
        }

        var temporaryDirectoryPath = Path.GetDirectoryName(normalizedTemporaryPath)
            ?? throw new InvalidOperationException(
                $"Temporary file directory path could not be resolved: {normalizedTemporaryPath}");
        var destinationDirectoryPath = Path.GetDirectoryName(normalizedPath)
            ?? throw new InvalidOperationException(
                $"Destination directory path could not be resolved: {normalizedPath}");
        if (!PathIdentity.IsSamePath(temporaryDirectoryPath, destinationDirectoryPath))
        {
            throw new ArgumentException(
                "Atomic write temporary file and destination must share one directory.",
                nameof(temporaryPath));
        }

        if (!Path.GetFileName(normalizedTemporaryPath).StartsWith(
                AtomicWriteTemporaryFileNamePrefix,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Atomic write temporary file name is not owned by FileUtilities.",
                nameof(temporaryPath));
        }

        EnsureRegularFile(normalizedTemporaryPath, "Atomic write temporary file");
        EnsureWritableAtomicDestination(normalizedPath);
    }

    private static void ReplaceFileOnce (
        string temporaryPath,
        string path)
    {
        EnsureWritableAtomicDestination(path);
        try
        {
            File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (FileNotFoundException)
        {
            MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
        }
        catch (IOException) when (!File.Exists(path))
        {
            MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
        }
    }

    private static void MoveOrReplaceWhenCreatedConcurrently (
        string temporaryPath,
        string path)
    {
        try
        {
            File.Move(temporaryPath, path);
        }
        catch (IOException) when (File.Exists(path))
        {
            EnsureWritableAtomicDestination(path);
            File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
    }

    private static void EnsureWritableAtomicDestination (string path)
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

    /// <summary> Ensures an existing path is a regular file and not a reparse point. </summary>
    /// <param name="path"> The file path to inspect. </param>
    /// <param name="subject"> The subject included in a contract failure message. </param>
    /// <exception cref="FileNotFoundException"> Thrown when <paramref name="path" /> does not exist. </exception>
    /// <exception cref="IOException"> Thrown when <paramref name="path" /> is a directory or reparse point. </exception>
    internal static void EnsureRegularFile (
        string path,
        string subject)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException($"{subject} was not found: {path}", path);
        }
        catch (DirectoryNotFoundException)
        {
            throw new FileNotFoundException($"{subject} was not found: {path}", path);
        }
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"{subject} must not be a reparse point: {path}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"{subject} must not be a directory: {path}");
        }

        if (!FileSystemNodeClassifier.IsRegularFile(path, attributes))
        {
            throw new IOException($"{subject} must be a regular file: {path}");
        }
    }
}
