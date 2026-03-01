using System.Text;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements daemon log tail reads from fingerprint-local daemon log files. </summary>
internal sealed class DaemonLogReader : IDaemonLogReader
{
    /// <summary> Gets the default maximum byte count for daemon log tail reads. </summary>
    public const int DefaultMaxBytes = 65536;

    /// <summary> Reads the tail segment of daemon log file for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="maxBytes"> The maximum number of bytes to read from the end of daemon log file. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon log read result. </returns>
    public async ValueTask<DaemonLogReadResult> ReadTail (
        string storageRoot,
        string projectFingerprint,
        int maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string daemonLogPath;
        try
        {
            daemonLogPath = DaemonStoragePathResolver.ResolveDaemonLogPath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (IsPathFormatException(exception))
        {
            return DaemonLogReadResult.Failure(string.Empty, ExecutionError.InvalidArgument(
                $"Daemon log path is invalid. {exception.Message}"));
        }

        if (maxBytes <= 0)
        {
            return DaemonLogReadResult.Failure(daemonLogPath, ExecutionError.InvalidArgument(
                $"Daemon log maxBytes must be greater than zero. Actual: {maxBytes}."));
        }

        if (!File.Exists(daemonLogPath))
        {
            return DaemonLogReadResult.Success(
                text: string.Empty,
                truncated: false,
                path: daemonLogPath,
                sizeBytes: 0);
        }

        try
        {
            await using var stream = new FileStream(
                daemonLogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);

            var sizeBytes = stream.Length;
            if (sizeBytes == 0)
            {
                return DaemonLogReadResult.Success(
                    text: string.Empty,
                    truncated: false,
                    path: daemonLogPath,
                    sizeBytes: 0);
            }

            var bytesToRead = (int)Math.Min(sizeBytes, maxBytes);
            var truncated = sizeBytes > maxBytes;
            var offset = sizeBytes - bytesToRead;
            stream.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[bytesToRead];
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var readCount = await stream.ReadAsync(
                        buffer.AsMemory(totalRead, buffer.Length - totalRead),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (readCount == 0)
                {
                    break;
                }

                totalRead += readCount;
            }

            var text = Encoding.UTF8.GetString(buffer, 0, totalRead);
            return DaemonLogReadResult.Success(
                text: text,
                truncated: truncated,
                path: daemonLogPath,
                sizeBytes: sizeBytes);
        }
        catch (FileNotFoundException)
        {
            return DaemonLogReadResult.Success(
                text: string.Empty,
                truncated: false,
                path: daemonLogPath,
                sizeBytes: 0);
        }
        catch (DirectoryNotFoundException)
        {
            return DaemonLogReadResult.Success(
                text: string.Empty,
                truncated: false,
                path: daemonLogPath,
                sizeBytes: 0);
        }
        catch (Exception exception) when (IsPathFormatException(exception))
        {
            return DaemonLogReadResult.Failure(daemonLogPath, ExecutionError.InvalidArgument(
                $"Daemon log path is invalid: {daemonLogPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return DaemonLogReadResult.Failure(daemonLogPath, ExecutionError.InternalError(
                $"Failed to read daemon log file: {daemonLogPath}. {exception.Message}"));
        }
    }

    /// <summary> Determines whether one exception indicates invalid path formatting. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception indicates invalid path formatting; otherwise <see langword="false" />. </returns>
    private static bool IsPathFormatException (Exception exception)
    {
        return exception is ArgumentException
            or NotSupportedException
            or PathTooLongException;
    }

    /// <summary> Determines whether one exception indicates filesystem I/O failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception indicates I/O failure; otherwise <see langword="false" />. </returns>
    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException;
    }
}
