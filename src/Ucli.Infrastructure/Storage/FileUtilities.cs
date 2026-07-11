using System.Buffers;
using System.Text;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Storage;

/// <summary> Provides shared utility operations for filesystem files. </summary>
public static class FileUtilities
{
    private const int FileReadBufferSize = 4096;

    private const int TemporaryFileTokenLength = 12;

    private const int FileReplacementRetryLimit = 20;

    private const int FileReplacementRetryDelayMilliseconds = 5;

    private const int WindowsSharingViolationHResult = unchecked((int)0x80070020);

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

    /// <summary> Opens a read handle that does not block concurrent atomic file replacement. </summary>
    /// <param name="path"> The target file path. </param>
    /// <returns> The asynchronous sequential-read stream owned by the caller. </returns>
    internal static FileStream OpenReopenSafeReadStream (string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("path must not be empty.", nameof(path));
        }

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
        var temporaryPath = CreateTemporaryPath(directoryPath);

        try
        {
            await File.WriteAllTextAsync(temporaryPath, contents, cancellationToken).ConfigureAwait(false);
            await ReplaceFileWithRetryAsync(temporaryPath, path, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteIfExists(temporaryPath);
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
        var temporaryPath = CreateTemporaryPath(directoryPath);

        try
        {
            File.WriteAllText(temporaryPath, contents);
            ReplaceFileWithRetry(temporaryPath, path);
        }
        finally
        {
            DeleteIfExists(temporaryPath);
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

    /// <summary> Resolves the bounded retry delay for one failed atomic replacement attempt. </summary>
    /// <param name="exception"> The I/O failure raised by the replacement operation. </param>
    /// <param name="failureCount"> The one-based count of consecutive replacement failures. </param>
    /// <returns> The retry delay for a Windows sharing violation within the retry limit; otherwise <see langword="null" />. </returns>
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

        if (exception.HResult != WindowsSharingViolationHResult
            || failureCount > FileReplacementRetryLimit)
        {
            return null;
        }

        return TimeSpan.FromMilliseconds(FileReplacementRetryDelayMilliseconds * failureCount);
    }

    private static async ValueTask ReplaceFileWithRetryAsync (
        string temporaryPath,
        string path,
        CancellationToken cancellationToken)
    {
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

    private static string CreateTemporaryPath (string directoryPath)
    {
        var token = Guid.NewGuid().ToString("N")[..TemporaryFileTokenLength];
        return Path.Combine(directoryPath, ".tmp-" + token);
    }

    private static void ReplaceFileWithRetry (
        string temporaryPath,
        string path)
    {
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

    private static void ReplaceFileOnce (
        string temporaryPath,
        string path)
    {
        try
        {
            File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (FileNotFoundException)
        {
            try
            {
                File.Move(temporaryPath, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
        }
    }
}
