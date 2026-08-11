using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Screenshot.Artifacts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Screenshot.Artifacts.Png;
using MackySoft.Ucli.Infrastructure.Artifacts;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Screenshot.Artifacts;

/// <summary> Commits host-encoded screenshot PNG artifacts to fingerprint-scoped local storage. </summary>
internal sealed class FileScreenshotArtifactStore : IScreenshotArtifactStore
{
    private const int FileStreamBufferSize = 81920;
    private readonly Rgba8SrgbPngEncoder pngEncoder;
    private readonly Rgba8SrgbPngValidator pngValidator;
    private readonly ImmutableArtifactFilePublisher artifactPublisher;
    private readonly Action<AbsolutePath> ensureSecureStagingDirectory;

    /// <summary> Initializes a new screenshot artifact store. </summary>
    public FileScreenshotArtifactStore (
        Rgba8SrgbPngEncoder pngEncoder,
        Rgba8SrgbPngValidator pngValidator,
        ImmutableArtifactFilePublisher artifactPublisher,
        Action<AbsolutePath> ensureSecureStagingDirectory)
    {
        this.pngEncoder = pngEncoder ?? throw new ArgumentNullException(nameof(pngEncoder));
        this.pngValidator = pngValidator ?? throw new ArgumentNullException(nameof(pngValidator));
        this.artifactPublisher = artifactPublisher ?? throw new ArgumentNullException(nameof(artifactPublisher));
        this.ensureSecureStagingDirectory = ensureSecureStagingDirectory
            ?? throw new ArgumentNullException(nameof(ensureSecureStagingDirectory));
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

        PathArtifactRef? artifact = null;
        ExecutionError? error = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemAccessBoundary.EnsureSecureDirectory(paths.StagingDirectory);
            EnsureReadableRawStagingFile(paths.RawStagingPath, staging.SizeBytes);
            EnsureCapturePathDoesNotExist(paths.ArtifactDirectory, "Screenshot artifact directory");
            FileSystemAccessBoundary.EnsureSecureDirectory(paths.ArtifactDirectory);

            var publication = artifactPublisher.PublishAsync(
                new ArtifactKind(TextVocabulary.GetText(ScreenshotArtifactKind.Screenshot)),
                new ArtifactMediaType(TextVocabulary.GetText(ScreenshotArtifactMediaType.Png)),
                ContainedPath.Create(paths.RepositoryRoot, paths.PngPath),
                (pngStream, token) => EncodeTemporaryPngAsync(
                    paths,
                    staging,
                    pngStream,
                    token),
                (pngStream, token) => ValidateAndDiscardStagingAsync(
                    paths,
                    staging,
                    pngStream,
                    token),
                cancellationToken);
            artifact = await publication
                .ConfigureAwait(false);
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
            var discardResult = DiscardCore(paths);
            if (!discardResult.IsSuccess)
            {
                cleanupFailures.Add(discardResult.Error!.Message);
            }

            if (artifact == null)
            {
                TryDeleteArtifactDirectoryWhenEmpty(paths, cleanupFailures);
            }

            if (artifact == null && cleanupFailures.Count != 0)
            {
                var cleanupMessage = string.Join(" ", cleanupFailures);
                error = ExecutionError.InternalError(
                    error == null
                        ? $"Screenshot artifact cleanup failed. {cleanupMessage}"
                        : $"{error.Message} Screenshot artifact cleanup also failed. {cleanupMessage}");
            }
        }

        return artifact != null
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
                staging.Dimensions.Width,
                staging.Dimensions.Height,
                pngStream,
                cancellationToken)
            .ConfigureAwait(false);
        await pngStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ValidatePngAgainstRawAsync (
        CapturePaths paths,
        IpcScreenshotStagingImage staging,
        Stream pngStream,
        CancellationToken cancellationToken)
    {
        EnsureReadableRawStagingFile(paths.RawStagingPath, staging.SizeBytes);
        await using var rawStream = OpenRawStagingFile(paths.RawStagingPath);
        await pngValidator
            .ValidateAsync(
                pngStream,
                rawStream,
                staging.Dimensions.Width,
                staging.Dimensions.Height,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ValidateAndDiscardStagingAsync (
        CapturePaths paths,
        IpcScreenshotStagingImage staging,
        Stream pngStream,
        CancellationToken cancellationToken)
    {
        await ValidatePngAgainstRawAsync(
            paths,
            staging,
            pngStream,
            cancellationToken).ConfigureAwait(false);
        var discardResult = DiscardStagingCore(paths);
        if (!discardResult.IsSuccess)
        {
            throw new InvalidOperationException(discardResult.Error!.Message);
        }
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
            repositoryRoot,
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

    private static ScreenshotArtifactDiscardResult DiscardStagingCore (CapturePaths paths)
    {
        try
        {
            DeleteStagingLayout(paths);
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
        AbsolutePath RepositoryRoot,
        AbsolutePath LocalStorageDirectory,
        AbsolutePath ArtifactDirectory,
        AbsolutePath PngPath,
        AbsolutePath StagingDirectory,
        AbsolutePath RawStagingPath);

}
