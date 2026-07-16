using System.Buffers;
using System.Security.Cryptography;
using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Screenshot.Artifacts.Png;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Screenshot.Artifacts;

/// <summary> Commits host-encoded screenshot PNG artifacts to fingerprint-scoped local storage. </summary>
internal sealed class FileScreenshotArtifactStore : IScreenshotArtifactStore
{
    private const int FileStreamBufferSize = 81920;
    private readonly Rgba8SrgbPngEncoder pngEncoder;
    private readonly Rgba8SrgbPngValidator pngValidator;
    private readonly TimeProvider timeProvider;
    private readonly Action<string> ensureSecureStagingDirectory;
    private readonly Action<string> deleteOwnedFile;

    /// <summary> Initializes a new screenshot artifact store. </summary>
    public FileScreenshotArtifactStore (
        Rgba8SrgbPngEncoder pngEncoder,
        Rgba8SrgbPngValidator pngValidator,
        TimeProvider timeProvider,
        Action<string> ensureSecureStagingDirectory,
        Action<string> deleteOwnedFile)
    {
        this.pngEncoder = pngEncoder ?? throw new ArgumentNullException(nameof(pngEncoder));
        this.pngValidator = pngValidator ?? throw new ArgumentNullException(nameof(pngValidator));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.ensureSecureStagingDirectory = ensureSecureStagingDirectory
            ?? throw new ArgumentNullException(nameof(ensureSecureStagingDirectory));
        this.deleteOwnedFile = deleteOwnedFile ?? throw new ArgumentNullException(nameof(deleteOwnedFile));
    }

    /// <inheritdoc />
    public ScreenshotArtifactPreparationResult Prepare (
        ResolvedUnityProjectContext unityProject,
        Guid captureId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        if (captureId == Guid.Empty)
        {
            throw new ArgumentException("Capture identifier must not be empty.", nameof(captureId));
        }

        CapturePaths paths;
        try
        {
            paths = ResolvePaths(unityProject, captureId);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ScreenshotArtifactPreparationResult.Failure(ExecutionError.InvalidArgument(
                $"Screenshot artifact path is invalid. {exception.Message}"));
        }
        catch (InvalidOperationException exception)
        {
            return ScreenshotArtifactPreparationResult.Failure(ExecutionError.InvalidArgument(
                $"Screenshot artifact path layout is invalid. {exception.Message}"));
        }

        var stagingPreparationStarted = false;
        try
        {
            EnsureCapturePathDoesNotExist(paths.ArtifactDirectory, "Screenshot artifact directory");
            EnsureCapturePathDoesNotExist(paths.StagingDirectory, "Screenshot staging directory");
            stagingPreparationStarted = true;
            ensureSecureStagingDirectory(paths.StagingDirectory);
            return ScreenshotArtifactPreparationResult.Success(new ScreenshotArtifactLease(this, paths));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            var cleanupFailures = new List<string>();
            if (stagingPreparationStarted)
            {
                TryRollbackPreparedStagingDirectory(paths, cleanupFailures);
            }

            var cleanupMessage = cleanupFailures.Count == 0
                ? string.Empty
                : $" Screenshot staging rollback also failed. {string.Join(" ", cleanupFailures)}";
            return ScreenshotArtifactPreparationResult.Failure(ExecutionError.InternalError(
                $"Failed to prepare screenshot artifact storage. {exception.Message}{cleanupMessage}"));
        }
    }

    private async ValueTask<ScreenshotArtifactCommitResult> CommitAsync (
        CapturePaths paths,
        IpcScreenshotStagingImage staging,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(staging);

        string? temporaryPngPath = null;
        var finalArtifactCreated = false;
        ScreenshotArtifact? artifact = null;
        ExecutionError? error = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemAccessBoundary.EnsureSecureDirectory(paths.StagingDirectory);
            EnsureReadableRawStagingFile(paths.RawStagingPath, staging.SizeBytes);
            EnsureCapturePathDoesNotExist(paths.ArtifactDirectory, "Screenshot artifact directory");
            FileSystemAccessBoundary.EnsureSecureDirectory(paths.ArtifactDirectory);
            EnsureWritableNewFilePath(paths.PngPath, "Screenshot PNG artifact");

            var temporaryPngStream = FileUtilities.OpenAtomicWriteTemporaryFileInDirectory(
                paths.ArtifactDirectory,
                out var reservedTemporaryPngPath);
            temporaryPngPath = reservedTemporaryPngPath;
            using (temporaryPngStream)
            {
                await EncodeTemporaryPngAsync(
                        paths,
                        staging,
                        temporaryPngStream,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            FileSystemAccessBoundary.EnsureSecureFile(temporaryPngPath);
            await ValidatePngAgainstRawAsync(paths, staging, temporaryPngPath, cancellationToken).ConfigureAwait(false);

            File.Move(temporaryPngPath, paths.PngPath);
            temporaryPngPath = null;
            finalArtifactCreated = true;
            FileSystemAccessBoundary.EnsureSecureFile(paths.PngPath);
            await ValidatePngAgainstRawAsync(paths, staging, paths.PngPath, cancellationToken).ConfigureAwait(false);

            var committedFile = await ComputeCommittedFileAsync(paths.PngPath, cancellationToken).ConfigureAwait(false);
            artifact = new ScreenshotArtifact(
                paths.RepositoryRelativeArtifactPath,
                committedFile.Digest,
                committedFile.SizeBytes,
                timeProvider.GetUtcNow());
        }
        catch (ScreenshotCaptureContractException exception)
        {
            error = ExecutionError.InternalError(
                $"Screenshot staging contract is unsupported. {exception.Message}",
                ScreenshotErrorCodes.ScreenshotCaptureUnsupported);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            error = ExecutionError.InvalidArgument($"Screenshot artifact path is invalid. {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            error = ExecutionError.InternalError($"Failed to commit screenshot PNG artifact. {exception.Message}");
        }
        finally
        {
            var cleanupFailures = new List<string>();
            if (temporaryPngPath != null)
            {
                TryDeleteOwnedFileIfExists(
                    temporaryPngPath,
                    "temporary PNG artifact",
                    cleanupFailures);
            }

            var discardResult = DiscardCore(paths);
            if (!discardResult.IsSuccess)
            {
                cleanupFailures.Add(discardResult.Error!.Message);
            }

            if (finalArtifactCreated
                && (artifact == null || cleanupFailures.Count != 0))
            {
                TryDeleteOwnedFileIfExists(
                    paths.PngPath,
                    "uncommitted final PNG artifact",
                    cleanupFailures);
                artifact = null;
            }

            if (artifact == null)
            {
                TryDeleteArtifactDirectoryWhenEmpty(paths, cleanupFailures);
            }

            if (cleanupFailures.Count != 0)
            {
                var cleanupMessage = string.Join(" ", cleanupFailures);
                error = ExecutionError.InternalError(
                    error == null
                        ? $"Screenshot artifact cleanup failed. {cleanupMessage}"
                        : $"{error.Message} Screenshot artifact cleanup also failed. {cleanupMessage}");
            }
        }

        return artifact != null && error == null
            ? ScreenshotArtifactCommitResult.Success(artifact)
            : ScreenshotArtifactCommitResult.Failure(error
                ?? ExecutionError.InternalError("Screenshot artifact commit failed without a diagnostic."));
    }

    private async ValueTask EncodeTemporaryPngAsync (
        CapturePaths paths,
        IpcScreenshotStagingImage staging,
        Stream pngStream,
        CancellationToken cancellationToken)
    {
        await using var rawStream = OpenRawStagingFile(paths.RawStagingPath);
        await pngEncoder
            .EncodeAsync(
                rawStream,
                staging.Width,
                staging.Height,
                pngStream,
                cancellationToken)
            .ConfigureAwait(false);
        await pngStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ValidatePngAgainstRawAsync (
        CapturePaths paths,
        IpcScreenshotStagingImage staging,
        string pngPath,
        CancellationToken cancellationToken)
    {
        EnsureReadablePngFile(pngPath);
        EnsureReadableRawStagingFile(paths.RawStagingPath, staging.SizeBytes);
        await using var pngStream = new FileStream(
            pngPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var rawStream = OpenRawStagingFile(paths.RawStagingPath);
        await pngValidator
            .ValidateAsync(
                pngStream,
                rawStream,
                staging.Width,
                staging.Height,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static FileStream OpenRawStagingFile (string path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static CapturePaths ResolvePaths (
        ResolvedUnityProjectContext unityProject,
        Guid captureId)
    {
        var repositoryRoot = unityProject.RepositoryRoot;
        var localStorageDirectory = Path.GetFullPath(
            UcliStoragePathResolver.ResolveLocalDirectoryPath(repositoryRoot));
        var artifactDirectory = Path.GetFullPath(
            UcliStoragePathResolver.ResolveScreenshotCaptureArtifactsDirectory(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                captureId));
        var pngPath = Path.GetFullPath(
            UcliStoragePathResolver.ResolveScreenshotCaptureArtifactPath(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                captureId));
        var stagingDirectory = Path.GetFullPath(
            UcliStoragePathResolver.ResolveScreenshotCaptureStagingDirectory(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                captureId));
        var rawStagingPath = Path.GetFullPath(
            UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
                repositoryRoot,
                unityProject.ProjectFingerprint,
                captureId));

        EnsureContainedPath(repositoryRoot, localStorageDirectory, "local storage directory");
        EnsureContainedPath(localStorageDirectory, artifactDirectory, "artifact directory");
        EnsureContainedPath(localStorageDirectory, stagingDirectory, "staging directory");
        EnsureContainedPath(artifactDirectory, pngPath, "PNG artifact");
        EnsureContainedPath(stagingDirectory, rawStagingPath, "raw staging file");

        return new CapturePaths(
            ResolveRepositoryRelativeArtifactPath(repositoryRoot, pngPath),
            localStorageDirectory,
            artifactDirectory,
            pngPath,
            stagingDirectory,
            rawStagingPath);
    }

    private static void EnsureContainedPath (
        string boundaryPath,
        string candidatePath,
        string pathKind)
    {
        if (!PathIdentity.IsChildPath(boundaryPath, candidatePath))
        {
            throw new InvalidOperationException(
                $"Screenshot {pathKind} path must remain under its owned directory. Boundary={boundaryPath}, Target={candidatePath}");
        }
    }

    private static string ResolveRepositoryRelativeArtifactPath (
        string repositoryRoot,
        string pngPath)
    {
        var result = RepositoryPathNormalizer.TryNormalize(repositoryRoot, pngPath);
        if (!result.IsSuccess || string.Equals(result.RepositoryRelativeSlashPath, ".", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Screenshot artifact path must resolve inside the repository root: {pngPath}");
        }

        return result.RepositoryRelativeSlashPath!;
    }

    private static void EnsureCapturePathDoesNotExist (
        string path,
        string description)
    {
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new IOException($"{description} already exists: {path}");
        }
    }

    private static void EnsureWritableNewFilePath (
        string path,
        string description)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"{description} must not be a reparse point: {path}");
        }

        throw new IOException($"{description} already exists: {path}");
    }

    private static void EnsureReadableRawStagingFile (
        string path,
        long expectedSizeBytes)
    {
        EnsureReadableRegularFile(path, "Raw screenshot staging file");
        var actualSizeBytes = new FileInfo(path).Length;
        if (actualSizeBytes != expectedSizeBytes)
        {
            throw new ScreenshotCaptureContractException(
                $"Raw staging file length does not match capture metadata. Expected={expectedSizeBytes}, Actual={actualSizeBytes}.");
        }
    }

    private static void EnsureReadablePngFile (string path)
    {
        EnsureReadableRegularFile(path, "Screenshot PNG artifact");
    }

    private static void EnsureReadableRegularFile (
        string path,
        string description)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException($"{description} was not found: {path}", path);
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"{description} must not be a reparse point: {path}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"{description} must not be a directory: {path}");
        }

        if (!FileSystemNodeClassifier.IsRegularFile(path, attributes))
        {
            throw new IOException($"{description} must be a regular file: {path}");
        }
    }

    private static async ValueTask<CommittedFile> ComputeCommittedFileAsync (
        string path,
        CancellationToken cancellationToken)
    {
        EnsureReadablePngFile(path);
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var expectedSizeBytes = stream.Length;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(FileStreamBufferSize);
        long totalBytesRead = 0;
        try
        {
            while (true)
            {
                var bytesRead = await stream
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
                hash.AppendData(buffer, 0, bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (totalBytesRead != expectedSizeBytes || stream.Length != expectedSizeBytes)
        {
            throw new IOException($"Screenshot PNG artifact length changed while hashing: {path}");
        }

        return new CommittedFile(
            expectedSizeBytes,
            Sha256LowerHex.GetHashAndReset(hash));
    }

    private static void TryRollbackPreparedStagingDirectory (
        CapturePaths paths,
        ICollection<string> cleanupFailures)
    {
        try
        {
            RollbackPreparedStagingDirectory(paths);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            cleanupFailures.Add($"Failed to remove the prepared screenshot staging directory. {exception.Message}");
        }
    }

    private static void RollbackPreparedStagingDirectory (CapturePaths paths)
    {
        if (!Directory.Exists(paths.StagingDirectory) && !File.Exists(paths.StagingDirectory))
        {
            return;
        }

        EnsureExistingDirectoryAncestorsAreNotReparsePoints(
            paths.LocalStorageDirectory,
            paths.StagingDirectory);

        if (Directory.Exists(paths.StagingDirectory))
        {
            var attributes = File.GetAttributes(paths.StagingDirectory);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(paths.StagingDirectory);
                return;
            }

            if (Directory.EnumerateFileSystemEntries(paths.StagingDirectory).Any())
            {
                throw new IOException(
                    $"Prepared screenshot staging directory contains unexpected entries: {paths.StagingDirectory}");
            }

            Directory.Delete(paths.StagingDirectory);
            return;
        }

        var fileAttributes = File.GetAttributes(paths.StagingDirectory);
        if ((fileAttributes & FileAttributes.ReparsePoint) != 0)
        {
            File.Delete(paths.StagingDirectory);
            return;
        }

        throw new IOException(
            $"Prepared screenshot staging directory path is occupied by an unexpected file: {paths.StagingDirectory}");
    }

    private static void EnsureExistingDirectoryAncestorsAreNotReparsePoints (
        string boundaryDirectory,
        string targetDirectory)
    {
        var targetParentDirectory = Path.GetDirectoryName(targetDirectory)
            ?? throw new InvalidOperationException($"Screenshot staging parent directory could not be resolved: {targetDirectory}");
        var pendingDirectories = new Stack<string>();
        var currentDirectory = targetParentDirectory;
        while (true)
        {
            pendingDirectories.Push(currentDirectory);
            if (PathIdentity.IsSamePath(currentDirectory, boundaryDirectory))
            {
                break;
            }

            currentDirectory = Path.GetDirectoryName(currentDirectory)
                ?? throw new InvalidOperationException(
                    $"Screenshot staging directory escaped its local storage boundary: {targetDirectory}");
        }

        while (pendingDirectories.Count != 0)
        {
            var directory = pendingDirectories.Pop();
            if (!Directory.Exists(directory))
            {
                if (File.Exists(directory))
                {
                    throw new IOException($"Screenshot staging ancestor is not a directory: {directory}");
                }

                throw new IOException($"Screenshot staging ancestor disappeared during rollback: {directory}");
            }

            var attributes = File.GetAttributes(directory);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Screenshot staging ancestor must not be a reparse point: {directory}");
            }
        }
    }

    private static ScreenshotArtifactDiscardResult DiscardCore (CapturePaths paths)
    {
        try
        {
            DeleteStagingLayout(paths);
            if (!File.Exists(paths.PngPath))
            {
                DeleteDirectoryWhenEmptyOrReparsePoint(paths.ArtifactDirectory, "Screenshot artifact directory");
            }

            return ScreenshotArtifactDiscardResult.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ScreenshotArtifactDiscardResult.Failure(ExecutionError.InternalError(
                $"Failed to discard screenshot staging files. {exception.Message}"));
        }
    }

    private static void DeleteStagingLayout (CapturePaths paths)
    {
        if (Directory.Exists(paths.StagingDirectory))
        {
            var attributes = File.GetAttributes(paths.StagingDirectory);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(paths.StagingDirectory);
                return;
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(paths.StagingDirectory);
            DeleteExpectedStagingFile(paths.RawStagingPath);
            DeleteDirectoryWhenEmptyOrReparsePoint(paths.StagingDirectory, "Screenshot staging directory");
            return;
        }

        if (!File.Exists(paths.StagingDirectory))
        {
            return;
        }

        var stagingAttributes = File.GetAttributes(paths.StagingDirectory);
        if ((stagingAttributes & FileAttributes.ReparsePoint) == 0)
        {
            throw new IOException($"Screenshot staging directory path is occupied by a file: {paths.StagingDirectory}");
        }

        File.Delete(paths.StagingDirectory);
    }

    private static void DeleteExpectedStagingFile (string path)
    {
        if (Directory.Exists(path))
        {
            throw new IOException($"Screenshot raw staging path must not be a directory: {path}");
        }

        try
        {
            File.Delete(path);
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private static void DeleteDirectoryWhenEmptyOrReparsePoint (
        string path,
        string description)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            Directory.Delete(path);
            return;
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(path);
        if (Directory.EnumerateFileSystemEntries(path).Any())
        {
            throw new IOException($"{description} contains unexpected files and cannot be removed safely: {path}");
        }

        Directory.Delete(path);
    }

    private void TryDeleteOwnedFileIfExists (
        string path,
        string description,
        ICollection<string> cleanupFailures)
    {
        try
        {
            deleteOwnedFile(path);
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            cleanupFailures.Add($"Failed to delete screenshot {description}. {exception.Message}");
        }
    }

    private static void TryDeleteArtifactDirectoryWhenEmpty (
        CapturePaths paths,
        ICollection<string> cleanupFailures)
    {
        if (File.Exists(paths.PngPath))
        {
            return;
        }

        try
        {
            DeleteDirectoryWhenEmptyOrReparsePoint(
                paths.ArtifactDirectory,
                "Screenshot artifact directory");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            cleanupFailures.Add($"Failed to remove the empty screenshot artifact directory. {exception.Message}");
        }
    }

    private sealed class ScreenshotCaptureContractException : Exception
    {
        public ScreenshotCaptureContractException (string message)
            : base(message)
        {
        }
    }

    private sealed class ScreenshotArtifactLease : IScreenshotArtifactLease
    {
        private readonly FileScreenshotArtifactStore store;
        private readonly CapturePaths paths;

        public ScreenshotArtifactLease (
            FileScreenshotArtifactStore store,
            CapturePaths paths)
        {
            this.store = store;
            this.paths = paths;
        }

        public ValueTask<ScreenshotArtifactCommitResult> CommitAsync (
            IpcScreenshotStagingImage staging,
            CancellationToken cancellationToken = default)
        {
            return store.CommitAsync(paths, staging, cancellationToken);
        }

        public ScreenshotArtifactDiscardResult Discard ()
        {
            return DiscardCore(paths);
        }
    }

    private sealed record CapturePaths (
        string RepositoryRelativeArtifactPath,
        string LocalStorageDirectory,
        string ArtifactDirectory,
        string PngPath,
        string StagingDirectory,
        string RawStagingPath);

    private readonly record struct CommittedFile (
        long SizeBytes,
        Sha256Digest Digest);
}
