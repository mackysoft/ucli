using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Staging
{
    /// <summary> Writes raw screenshot staging images through an adjacent temporary file. </summary>
    internal sealed class ScreenshotStagingImageWriter : IScreenshotStagingImageWriter
    {
        private readonly string storageRoot;

        private readonly ProjectFingerprint projectFingerprint;

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
            Guid captureId,
            ReadOnlyMemory<byte> bytes,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (captureId == Guid.Empty)
            {
                throw new ArgumentException("Capture id must not be empty.", nameof(captureId));
            }

            var fullPath = ResolvePreparedStagingPath(captureId);
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

            var temporaryStream = FileUtilities.OpenAtomicWriteTemporaryFileInDirectory(
                directoryPath,
                out var temporaryPath);
            var published = false;
            try
            {
                using (temporaryStream)
                {
                    await temporaryStream.WriteAsync(bytes, cancellationToken);
                    await temporaryStream.FlushAsync(cancellationToken);
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
                    DeletePathIfExists(fullPath);
                }

                throw;
            }
            finally
            {
                if (!published)
                {
                    DeletePathIfExists(temporaryPath);
                }
            }
        }

        /// <inheritdoc />
        public void DeleteIfExists (Guid captureId)
        {
            if (captureId == Guid.Empty)
            {
                throw new ArgumentException("Capture id must not be empty.", nameof(captureId));
            }

            var path = UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
                storageRoot,
                projectFingerprint,
                captureId);
            DeletePathIfExists(path);
        }

        private string ResolvePreparedStagingPath (Guid captureId)
        {
            var fullPath = Path.GetFullPath(
                UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
                    storageRoot,
                    projectFingerprint,
                    captureId));
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

        private static void DeletePathIfExists (string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
