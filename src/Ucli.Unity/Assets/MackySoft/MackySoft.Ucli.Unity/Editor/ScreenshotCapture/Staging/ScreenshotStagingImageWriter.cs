using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Staging
{
    /// <summary> Writes raw screenshot staging images through an adjacent temporary file. </summary>
    internal sealed class ScreenshotStagingImageWriter : IScreenshotStagingImageWriter
    {
        private readonly string storageRoot;

        private readonly string projectFingerprint;

        private readonly string screenshotWorkDirectory;

        /// <summary> Initializes a writer scoped to the project fingerprint served by this daemon. </summary>
        public ScreenshotStagingImageWriter (IpcProjectIdentity projectIdentity)
        {
            if (projectIdentity == null)
            {
                throw new ArgumentNullException(nameof(projectIdentity));
            }

            storageRoot = UcliStoragePathResolver.ResolveStorageRoot(projectIdentity.ProjectPath);
            projectFingerprint = projectIdentity.ProjectFingerprint;
            screenshotWorkDirectory = UcliStoragePathResolver.ResolveScreenshotWorkDirectory(
                storageRoot,
                projectFingerprint);
        }

        /// <inheritdoc />
        public async Task<long> WriteAtomicAsync (
            string path,
            ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Screenshot staging path must not be empty.", nameof(path));
            }

            var fullPath = ValidatePreparedStagingPath(path);
            var directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new InvalidOperationException(
                    "Validated screenshot staging path has no parent directory.");
            }

            FileSystemAccessBoundary.EnsureSecureDirectoryChain(
                screenshotWorkDirectory,
                directoryPath);
            EnsureTargetDoesNotExist(fullPath);

            var temporaryPath = fullPath + $".tmp.{Guid.NewGuid():N}";
            var published = false;
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true))
                {
                    await stream.WriteAsync(bytes, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                FileSystemAccessBoundary.EnsureSecureFile(temporaryPath);
                File.Move(temporaryPath, fullPath);
                published = true;
                FileSystemAccessBoundary.EnsureSecureFile(fullPath);
                return bytes.Length;
            }
            catch
            {
                if (published)
                {
                    DeleteIfExists(fullPath);
                }

                throw;
            }
            finally
            {
                DeleteIfExists(temporaryPath);
            }
        }

        /// <inheritdoc />
        public void DeleteIfExists (string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private string ValidatePreparedStagingPath (string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (!Path.IsPathRooted(path)
                || !string.Equals(path, fullPath, StringComparison.Ordinal))
            {
                throw new ArgumentException("Screenshot staging path must be absolute and normalized.", nameof(path));
            }

            if (!string.Equals(
                Path.GetFileName(fullPath),
                UcliStoragePathNames.ScreenshotRawStagingFileName,
                StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Screenshot staging file name must be '{UcliStoragePathNames.ScreenshotRawStagingFileName}'.",
                    nameof(path));
            }

            var directoryPath = Path.GetDirectoryName(fullPath);
            var parentPath = string.IsNullOrWhiteSpace(directoryPath)
                ? null
                : Path.GetDirectoryName(directoryPath);
            if (string.IsNullOrWhiteSpace(directoryPath)
                || string.IsNullOrWhiteSpace(parentPath)
                || !PathIdentity.IsSamePath(parentPath, screenshotWorkDirectory)
                || !Directory.Exists(screenshotWorkDirectory)
                || !Directory.Exists(directoryPath))
            {
                throw new IOException(
                    "Screenshot staging path must be inside one existing capture directory owned by this project fingerprint.");
            }

            var captureId = Path.GetFileName(directoryPath);
            var expectedDirectoryPath = UcliStoragePathResolver.ResolveScreenshotCaptureStagingDirectory(
                storageRoot,
                projectFingerprint,
                captureId);
            if (!PathIdentity.IsSamePath(directoryPath, expectedDirectoryPath))
            {
                throw new IOException("Screenshot staging capture directory is not normalized.");
            }

            return fullPath;
        }

        private static void EnsureTargetDoesNotExist (string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Screenshot staging target must not be a reparse point: {path}");
            }

            throw new IOException($"Screenshot staging target already exists: {path}");
        }
    }
}
