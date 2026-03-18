using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements byte-range export from Unity editor log file. </summary>
    internal sealed class EditorLogRangeExporter : IEditorLogRangeExporter
    {
        private const int BufferSize = 81920;

        /// <summary> Exports one half-open byte range from source log file into destination file. </summary>
        /// <param name="sourcePath"> The source log file path. </param>
        /// <param name="destinationPath"> The destination artifact file path. </param>
        /// <param name="startOffset"> The inclusive start byte offset. </param>
        /// <param name="endOffset"> The exclusive end byte offset. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
        public async Task ExportRange (
            string sourcePath,
            string destinationPath,
            long startOffset,
            long endOffset,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path must not be null or whitespace.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("Destination path must not be null or whitespace.", nameof(destinationPath));
            }

            if (startOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startOffset), "Start offset must be greater than or equal to zero.");
            }

            if (endOffset < startOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(endOffset), "End offset must be greater than or equal to start offset.");
            }

            var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
            {
                throw new InvalidOperationException($"Destination directory path could not be resolved: {destinationPath}");
            }

            UcliLocalStorageBootstrapper.EnsureInitialized(destinationDirectoryPath);
            Directory.CreateDirectory(destinationDirectoryPath);
            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferSize, true);
            if (startOffset > sourceStream.Length || endOffset > sourceStream.Length)
            {
                throw new InvalidOperationException(
                    $"Editor log offset is out of range. start={startOffset}, end={endOffset}, length={sourceStream.Length}.");
            }

            using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

            sourceStream.Seek(startOffset, SeekOrigin.Begin);
            var remaining = endOffset - startOffset;
            var buffer = new byte[BufferSize];
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var readLength = remaining > buffer.Length
                    ? buffer.Length
                    : (int)remaining;
                var bytesRead = await sourceStream.ReadAsync(buffer, 0, readLength, cancellationToken);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException("Unexpected end of editor log while exporting selected byte range.");
                }

                await destinationStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                remaining -= bytesRead;
            }
        }
    }
}