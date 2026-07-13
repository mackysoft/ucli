using System.Text;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Logs;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.Logs;

/// <summary> Implements Unity log tail reads from fingerprint-local Unity log files. </summary>
internal sealed class UnityLogReader : IUnityLogReader
{
    /// <summary> Gets the default maximum byte count for Unity log tail reads. </summary>
    public const int DefaultMaxBytes = 65536;

    /// <summary> Reads the tail segment of Unity log file for one project fingerprint. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <param name="maxBytes"> The maximum number of bytes to read from the end of Unity log file. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The Unity log read result. </returns>
    public async ValueTask<UnityLogReadResult> ReadTailAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        int maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string unityLogPath;
        try
        {
            unityLogPath = UcliStoragePathResolver.ResolveUnityLogPath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityLogReadResult.Failure(string.Empty, ExecutionError.InvalidArgument(
                $"Unity log path is invalid. {exception.Message}"));
        }

        if (maxBytes <= 0)
        {
            return UnityLogReadResult.Failure(unityLogPath, ExecutionError.InvalidArgument(
                $"Unity log maxBytes must be greater than zero. Actual: {maxBytes}."));
        }

        if (!File.Exists(unityLogPath))
        {
            return UnityLogReadResult.Success(
                text: string.Empty,
                truncated: false,
                path: unityLogPath,
                sizeBytes: 0);
        }

        try
        {
            await using var stream = new FileStream(
                unityLogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                useAsync: true);

            var sizeBytes = stream.Length;
            if (sizeBytes == 0)
            {
                return UnityLogReadResult.Success(
                    text: string.Empty,
                    truncated: false,
                    path: unityLogPath,
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
            return UnityLogReadResult.Success(
                text: text,
                truncated: truncated,
                path: unityLogPath,
                sizeBytes: sizeBytes);
        }
        catch (FileNotFoundException)
        {
            return UnityLogReadResult.Success(
                text: string.Empty,
                truncated: false,
                path: unityLogPath,
                sizeBytes: 0);
        }
        catch (DirectoryNotFoundException)
        {
            return UnityLogReadResult.Success(
                text: string.Empty,
                truncated: false,
                path: unityLogPath,
                sizeBytes: 0);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityLogReadResult.Failure(unityLogPath, ExecutionError.InvalidArgument(
                $"Unity log path is invalid: {unityLogPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return UnityLogReadResult.Failure(unityLogPath, ExecutionError.InternalError(
                $"Failed to read Unity log file: {unityLogPath}. {exception.Message}"));
        }
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
