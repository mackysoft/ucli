using System.Buffers;
using System.Security.Cryptography;
using MackySoft.FileSystem;
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
    private readonly Action<AbsolutePath> ensureSecureStagingDirectory;
    private readonly Action<AbsolutePath> deleteOwnedFile;

    /// <summary> Initializes a new screenshot artifact store. </summary>
    public FileScreenshotArtifactStore (
        Rgba8SrgbPngEncoder pngEncoder,
        Rgba8SrgbPngValidator pngValidator,
        TimeProvider timeProvider,
        Action<AbsolutePath> ensureSecureStagingDirectory,
        Action<AbsolutePath> deleteOwnedFile)
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

        AbsolutePath? temporaryPngPath = null;
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

            File.Move(temporaryPngPath.Value, paths.PngPath.Value);
            temporaryPngPath = null;
            finalArtifactCreated = true;
            FileSystemAccessBoundary.EnsureSecureFile(paths.PngPath);
            await ValidatePngAgainstRawAsync(paths, staging, paths.PngPath, cancellationToken).ConfigureAwait(false);

            var committedFile = await ComputeCommittedFileAsync(paths.PngPath, cancellationToken).ConfigureAwait(false);
            if (!UcliPortablePathAdapter.TryFormat(
                    paths.RepositoryRelativeArtifactPath,
                    out var portableArtifactPath))
            {
                throw new InvalidOperationException(
                    $"Screenshot artifact path cannot be represented by the portable result contract: {paths.RepositoryRelativeArtifactPath.Value}");
            }

            artifact = new ScreenshotArtifact(
                portableArtifactPath,
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
        AbsolutePath pngPath,
        CancellationToken cancellationToken)
    {
        EnsureReadablePngFile(pngPath);
        EnsureReadableRawStagingFile(paths.RawStagingPath, staging.SizeBytes);
        await using var pngStream = new FileStream(
            pngPath.Value,
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

    private static FileStream OpenRawStagingFile (AbsolutePath path)
    {
        return new FileStream(
            path.Value,
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
        var localStorageDirectory = UcliStoragePathResolver.ResolveLocalDirectoryPath(repositoryRoot);
        var artifactDirectory = UcliStoragePathResolver.ResolveScreenshotCaptureArtifactsDirectory(
            repositoryRoot,
            unityProject.ProjectFingerprint,
            captureId);
        var pngPath = UcliStoragePathResolver.ResolveScreenshotCaptureArtifactPath(
            repositoryRoot,
            unityProject.ProjectFingerprint,
            captureId);
        var stagingDirectory = UcliStoragePathResolver.ResolveScreenshotCaptureStagingDirectory(
            repositoryRoot,
            unityProject.ProjectFingerprint,
            captureId);
        var rawStagingPath = UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
            repositoryRoot,
            unityProject.ProjectFingerprint,
            captureId);

        var localStorageRelation = ContainedPath.Create(repositoryRoot, localStorageDirectory);
        var artifactDirectoryRelation = ContainedPath.Create(localStorageDirectory, artifactDirectory);
        var stagingDirectoryRelation = ContainedPath.Create(localStorageDirectory, stagingDirectory);
        var pngRelation = ContainedPath.Create(artifactDirectory, pngPath);
        var rawStagingRelation = ContainedPath.Create(stagingDirectory, rawStagingPath);
        if (localStorageRelation.RelativePath.IsRoot
            || artifactDirectoryRelation.RelativePath.IsRoot
            || stagingDirectoryRelation.RelativePath.IsRoot
            || pngRelation.RelativePath.IsRoot
            || rawStagingRelation.RelativePath.IsRoot)
        {
            throw new InvalidOperationException("Screenshot storage layout paths must be descendants of their owned directories.");
        }

        return new CapturePaths(
            ContainedPath.Create(repositoryRoot, pngPath).RelativePath,
            localStorageDirectory,
            artifactDirectory,
            pngPath,
            stagingDirectory,
            rawStagingPath);
    }

    private static void EnsureCapturePathDoesNotExist (
        AbsolutePath path,
        string description)
    {
        if (File.Exists(path.Value) || Directory.Exists(path.Value))
        {
            throw new IOException($"{description} already exists: {path}");
        }
    }

    private static void EnsureWritableNewFilePath (
        AbsolutePath path,
        string description)
    {
        if (!File.Exists(path.Value) && !Directory.Exists(path.Value))
        {
            return;
        }

        var attributes = File.GetAttributes(path.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"{description} must not be a reparse point: {path}");
        }

        throw new IOException($"{description} already exists: {path}");
    }

    private static void EnsureReadableRawStagingFile (
        AbsolutePath path,
        long expectedSizeBytes)
    {
        EnsureReadableRegularFile(path, "Raw screenshot staging file");
        var actualSizeBytes = new FileInfo(path.Value).Length;
        if (actualSizeBytes != expectedSizeBytes)
        {
            throw new ScreenshotCaptureContractException(
                $"Raw staging file length does not match capture metadata. Expected={expectedSizeBytes}, Actual={actualSizeBytes}.");
        }
    }

    private static void EnsureReadablePngFile (AbsolutePath path)
    {
        EnsureReadableRegularFile(path, "Screenshot PNG artifact");
    }

    private static void EnsureReadableRegularFile (
        AbsolutePath path,
        string description)
    {
        if (!File.Exists(path.Value) && !Directory.Exists(path.Value))
        {
            throw new FileNotFoundException($"{description} was not found: {path}", path.Value);
        }

        var attributes = File.GetAttributes(path.Value);
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
        AbsolutePath path,
        CancellationToken cancellationToken)
    {
        EnsureReadablePngFile(path);
        await using var stream = new FileStream(
            path.Value,
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
        if (!Directory.Exists(paths.StagingDirectory.Value) && !File.Exists(paths.StagingDirectory.Value))
        {
            return;
        }

        EnsureExistingDirectoryAncestorsAreNotReparsePoints(
            paths.LocalStorageDirectory,
            paths.StagingDirectory);

        if (Directory.Exists(paths.StagingDirectory.Value))
        {
            var attributes = File.GetAttributes(paths.StagingDirectory.Value);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(paths.StagingDirectory.Value);
                return;
            }

            if (Directory.EnumerateFileSystemEntries(paths.StagingDirectory.Value).Any())
            {
                throw new IOException(
                    $"Prepared screenshot staging directory contains unexpected entries: {paths.StagingDirectory}");
            }

            Directory.Delete(paths.StagingDirectory.Value);
            return;
        }

        var fileAttributes = File.GetAttributes(paths.StagingDirectory.Value);
        if ((fileAttributes & FileAttributes.ReparsePoint) != 0)
        {
            File.Delete(paths.StagingDirectory.Value);
            return;
        }

        throw new IOException(
            $"Prepared screenshot staging directory path is occupied by an unexpected file: {paths.StagingDirectory}");
    }

    private static void EnsureExistingDirectoryAncestorsAreNotReparsePoints (
        AbsolutePath boundaryDirectory,
        AbsolutePath targetDirectory)
    {
        if (!targetDirectory.TryGetParent(out var targetParentDirectory))
        {
            throw new InvalidOperationException(
                $"Screenshot staging parent directory could not be resolved: {targetDirectory.Value}");
        }
        var pendingDirectories = new Stack<AbsolutePath>();
        var currentDirectory = targetParentDirectory;
        while (true)
        {
            pendingDirectories.Push(currentDirectory);
            if (currentDirectory == boundaryDirectory)
            {
                break;
            }

            if (!currentDirectory.TryGetParent(out var parentDirectory))
            {
                throw new InvalidOperationException(
                    $"Screenshot staging directory escaped its local storage boundary: {targetDirectory.Value}");
            }

            currentDirectory = parentDirectory;
        }

        while (pendingDirectories.Count != 0)
        {
            var directory = pendingDirectories.Pop();
            if (!Directory.Exists(directory.Value))
            {
                if (File.Exists(directory.Value))
                {
                    throw new IOException($"Screenshot staging ancestor is not a directory: {directory}");
                }

                throw new IOException($"Screenshot staging ancestor disappeared during rollback: {directory}");
            }

            var attributes = File.GetAttributes(directory.Value);
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
            if (!File.Exists(paths.PngPath.Value))
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
        if (Directory.Exists(paths.StagingDirectory.Value))
        {
            var attributes = File.GetAttributes(paths.StagingDirectory.Value);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(paths.StagingDirectory.Value);
                return;
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(paths.StagingDirectory);
            DeleteExpectedStagingFile(paths.RawStagingPath);
            DeleteDirectoryWhenEmptyOrReparsePoint(paths.StagingDirectory, "Screenshot staging directory");
            return;
        }

        if (!File.Exists(paths.StagingDirectory.Value))
        {
            return;
        }

        var stagingAttributes = File.GetAttributes(paths.StagingDirectory.Value);
        if ((stagingAttributes & FileAttributes.ReparsePoint) == 0)
        {
            throw new IOException($"Screenshot staging directory path is occupied by a file: {paths.StagingDirectory}");
        }

        File.Delete(paths.StagingDirectory.Value);
    }

    private static void DeleteExpectedStagingFile (AbsolutePath path)
    {
        if (Directory.Exists(path.Value))
        {
            throw new IOException($"Screenshot raw staging path must not be a directory: {path}");
        }

        try
        {
            File.Delete(path.Value);
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private static void DeleteDirectoryWhenEmptyOrReparsePoint (
        AbsolutePath path,
        string description)
    {
        if (!Directory.Exists(path.Value))
        {
            return;
        }

        var attributes = File.GetAttributes(path.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            Directory.Delete(path.Value);
            return;
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(path);
        if (Directory.EnumerateFileSystemEntries(path.Value).Any())
        {
            throw new IOException($"{description} contains unexpected files and cannot be removed safely: {path}");
        }

        Directory.Delete(path.Value);
    }

    private void TryDeleteOwnedFileIfExists (
        AbsolutePath path,
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
        if (File.Exists(paths.PngPath.Value))
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
        RootRelativePath RepositoryRelativeArtifactPath,
        AbsolutePath LocalStorageDirectory,
        AbsolutePath ArtifactDirectory,
        AbsolutePath PngPath,
        AbsolutePath StagingDirectory,
        AbsolutePath RawStagingPath);

    private readonly record struct CommittedFile (
        long SizeBytes,
        Sha256Digest Digest);
}
