using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Project;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Staging
{
    /// <summary> Writes raw screenshot staging images through an adjacent temporary file. </summary>
    internal sealed class ScreenshotStagingImageWriter : IScreenshotStagingImageWriter
    {
        private readonly AbsolutePath storageRoot;

        private readonly ProjectFingerprint projectFingerprint;

        private readonly AbsolutePath screenshotWorkDirectory;

        /// <summary> Initializes a writer scoped to the project fingerprint served by this daemon. </summary>
        public ScreenshotStagingImageWriter (UnityHostProjectIdentity projectIdentity)
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
            if (!fullPath.TryGetParent(out var directoryPath))
            {
                throw new InvalidOperationException(
                    "Validated screenshot staging path has no parent directory.");
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
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
                File.Move(temporaryPath.Value, fullPath.Value);
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

        private AbsolutePath ResolvePreparedStagingPath (Guid captureId)
        {
            var fullPath = UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
                storageRoot,
                projectFingerprint,
                captureId);
            if (!fullPath.TryGetParent(out var directoryPath))
            {
                throw new IOException(
                    "Screenshot staging path must be inside one existing capture directory owned by this project fingerprint.");
            }

            if (!directoryPath.TryGetParent(out var parentPath))
            {
                throw new IOException(
                    "Screenshot staging path must be inside one existing capture directory owned by this project fingerprint.");
            }

            if (!parentPath.IsSameAs(screenshotWorkDirectory)
                || !Directory.Exists(screenshotWorkDirectory.Value)
                || !Directory.Exists(directoryPath.Value))
            {
                throw new IOException(
                    "Screenshot staging path must be inside one existing capture directory owned by this project fingerprint.");
            }

            return fullPath;
        }

        private static void EnsureTargetDoesNotExist (AbsolutePath path)
        {
            if (!File.Exists(path.Value) && !Directory.Exists(path.Value))
            {
                return;
            }

            var attributes = File.GetAttributes(path.Value);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Screenshot staging target must not be a reparse point: {path.Value}");
            }

            throw new IOException($"Screenshot staging target already exists: {path.Value}");
        }

        private static void DeletePathIfExists (AbsolutePath path)
        {
            if (File.Exists(path.Value))
            {
                File.Delete(path.Value);
            }
        }
    }
}
