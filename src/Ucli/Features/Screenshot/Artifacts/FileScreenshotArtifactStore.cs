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
    private readonly Action<string> deleteOwnedFile;

    /// <summary> Initializes a new screenshot artifact store. </summary>
    public FileScreenshotArtifactStore (
        Rgba8SrgbPngEncoder pngEncoder,
        Rgba8SrgbPngValidator pngValidator,
        TimeProvider? timeProvider = null)
        : this(pngEncoder, pngValidator, timeProvider, File.Delete)
    {
    }

    /// <summary> Initializes a screenshot artifact store with explicit owned-file deletion for tests. </summary>
    internal FileScreenshotArtifactStore (
        Rgba8SrgbPngEncoder pngEncoder,
        Rgba8SrgbPngValidator pngValidator,
        TimeProvider? timeProvider,
        Action<string> deleteOwnedFile)
    {
        this.pngEncoder = pngEncoder ?? throw new ArgumentNullException(nameof(pngEncoder));
        this.pngValidator = pngValidator ?? throw new ArgumentNullException(nameof(pngValidator));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.deleteOwnedFile = deleteOwnedFile ?? throw new ArgumentNullException(nameof(deleteOwnedFile));
    }

    /// <inheritdoc />
    public ScreenshotArtifactPreparationResult Prepare (
        ResolvedUnityProjectContext unityProject,
        string captureId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(captureId);

        ScreenshotArtifactPaths paths;
        try
        {
            paths = ResolvePaths(unityProject, captureId);
            EnsureExpectedPathLayout(paths);
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

        try
        {
            EnsureCapturePathDoesNotExist(paths.ArtifactDirectory, "Screenshot artifact directory");
            EnsureCapturePathDoesNotExist(paths.StagingDirectory, "Screenshot staging directory");
            FileSystemAccessBoundary.EnsureSecureDirectory(paths.StagingDirectory);
            return ScreenshotArtifactPreparationResult.Success(paths);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ScreenshotArtifactPreparationResult.Failure(ExecutionError.InternalError(
                $"Failed to prepare screenshot artifact storage. {exception.Message}"));
        }
    }

    /// <inheritdoc />
    public async ValueTask<ScreenshotArtifactCommitResult> CommitAsync (
        ScreenshotArtifactCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Paths);

        string? temporaryPngPath = null;
        var layoutValidated = false;
        var finalArtifactCreated = false;
        ScreenshotArtifact? artifact = null;
        ExecutionError? error = null;
        try
        {
            EnsureExpectedPathLayout(request.Paths);
            layoutValidated = true;
            cancellationToken.ThrowIfCancellationRequested();
            temporaryPngPath = request.Paths.PngPath + $".tmp.{Guid.NewGuid():N}";
            ValidateStagingContract(request);
            FileSystemAccessBoundary.EnsureSecureDirectory(request.Paths.StagingDirectory);
            EnsureReadableRawStagingFile(request.Paths.RawStagingPath, request.SizeBytes);
            EnsureCapturePathDoesNotExist(request.Paths.ArtifactDirectory, "Screenshot artifact directory");
            FileSystemAccessBoundary.EnsureSecureDirectory(request.Paths.ArtifactDirectory);
            EnsureWritableNewFilePath(request.Paths.PngPath, "Screenshot PNG artifact");
            EnsureWritableNewFilePath(temporaryPngPath, "Screenshot temporary PNG artifact");

            await EncodeTemporaryPngAsync(request, temporaryPngPath, cancellationToken).ConfigureAwait(false);
            FileSystemAccessBoundary.EnsureSecureFile(temporaryPngPath);
            await ValidatePngAgainstRawAsync(request, temporaryPngPath, cancellationToken).ConfigureAwait(false);

            File.Move(temporaryPngPath, request.Paths.PngPath);
            finalArtifactCreated = true;
            FileSystemAccessBoundary.EnsureSecureFile(request.Paths.PngPath);
            await ValidatePngAgainstRawAsync(request, request.Paths.PngPath, cancellationToken).ConfigureAwait(false);

            var committedFile = await ComputeCommittedFileAsync(request.Paths.PngPath, cancellationToken).ConfigureAwait(false);
            artifact = new ScreenshotArtifact(
                NormalizeRepositoryRelativeArtifactPath(request.Paths),
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
        catch (InvalidOperationException exception) when (!layoutValidated)
        {
            error = ExecutionError.InvalidArgument($"Screenshot artifact path layout is invalid. {exception.Message}");
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

            var discardResult = layoutValidated
                ? DiscardCore(request.Paths)
                : ScreenshotArtifactDiscardResult.Success();
            if (!discardResult.IsSuccess)
            {
                cleanupFailures.Add(discardResult.Error!.Message);
            }

            if (finalArtifactCreated
                && (artifact == null || cleanupFailures.Count != 0))
            {
                TryDeleteOwnedFileIfExists(
                    request.Paths.PngPath,
                    "uncommitted final PNG artifact",
                    cleanupFailures);
                artifact = null;
            }

            if (layoutValidated && artifact == null)
            {
                TryDeleteArtifactDirectoryWhenEmpty(request.Paths, cleanupFailures);
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

    /// <inheritdoc />
    public ValueTask<ScreenshotArtifactDiscardResult> DiscardAsync (
        ScreenshotArtifactPaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        // NOTE: Discard is a compensation boundary and must remove staging even after the capture token is canceled.
        _ = cancellationToken;

        try
        {
            EnsureExpectedPathLayout(paths);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ValueTask.FromResult(ScreenshotArtifactDiscardResult.Failure(ExecutionError.InvalidArgument(
                $"Screenshot artifact path is invalid. {exception.Message}")));
        }
        catch (InvalidOperationException exception)
        {
            return ValueTask.FromResult(ScreenshotArtifactDiscardResult.Failure(ExecutionError.InvalidArgument(
                $"Screenshot artifact path layout is invalid. {exception.Message}")));
        }

        return ValueTask.FromResult(DiscardCore(paths));
    }

    private async ValueTask EncodeTemporaryPngAsync (
        ScreenshotArtifactCommitRequest request,
        string temporaryPngPath,
        CancellationToken cancellationToken)
    {
        await using var rawStream = OpenRawStagingFile(request.Paths.RawStagingPath);
        await using var pngStream = new FileStream(
            temporaryPngPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            FileStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await pngEncoder
            .EncodeAsync(rawStream, request.Width, request.Height, pngStream, cancellationToken)
            .ConfigureAwait(false);
        await pngStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ValidatePngAgainstRawAsync (
        ScreenshotArtifactCommitRequest request,
        string pngPath,
        CancellationToken cancellationToken)
    {
        EnsureReadablePngFile(pngPath);
        EnsureReadableRawStagingFile(request.Paths.RawStagingPath, request.SizeBytes);
        await using var pngStream = new FileStream(
            pngPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var rawStream = OpenRawStagingFile(request.Paths.RawStagingPath);
        await pngValidator
            .ValidateAsync(pngStream, rawStream, request.Width, request.Height, cancellationToken)
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

    private static void ValidateStagingContract (ScreenshotArtifactCommitRequest request)
    {
        string returnedStagingPath;
        try
        {
            returnedStagingPath = Path.GetFullPath(request.ReturnedStagingPath);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            throw new ScreenshotCaptureContractException($"Returned staging path is invalid. {exception.Message}");
        }

        if (!PathIdentity.IsSamePath(returnedStagingPath, request.Paths.RawStagingPath))
        {
            throw new ScreenshotCaptureContractException("Unity returned a staging path other than the host-prepared path.");
        }

        if (!string.Equals(request.PixelFormat, IpcScreenshotPixelFormatNames.Rgba8Srgb, StringComparison.Ordinal))
        {
            throw new ScreenshotCaptureContractException($"Pixel format must be {IpcScreenshotPixelFormatNames.Rgba8Srgb}: {request.PixelFormat}.");
        }

        if (!string.Equals(request.RowOrder, IpcScreenshotRowOrderNames.TopDown, StringComparison.Ordinal))
        {
            throw new ScreenshotCaptureContractException($"Row order must be {IpcScreenshotRowOrderNames.TopDown}: {request.RowOrder}.");
        }

        if (request.Width <= 0 || request.Height <= 0)
        {
            throw new ScreenshotCaptureContractException("Captured dimensions must be positive.");
        }

        if (request.Width > IpcScreenshotCaptureLimits.MaximumDimension
            || request.Height > IpcScreenshotCaptureLimits.MaximumDimension)
        {
            throw new ScreenshotCaptureContractException(
                $"Captured dimensions exceed the screenshot limit of {IpcScreenshotCaptureLimits.MaximumDimension} pixels per axis.");
        }

        int expectedRowStrideBytes;
        long expectedSizeBytes;
        try
        {
            expectedRowStrideBytes = checked(request.Width * 4);
            expectedSizeBytes = checked((long)expectedRowStrideBytes * request.Height);
        }
        catch (OverflowException exception)
        {
            throw new ScreenshotCaptureContractException($"Captured dimensions exceed the supported raw image size. {exception.Message}");
        }

        if (request.RowStrideBytes != expectedRowStrideBytes)
        {
            throw new ScreenshotCaptureContractException(
                $"Raw row stride does not match RGBA8 dimensions. Expected={expectedRowStrideBytes}, Actual={request.RowStrideBytes}.");
        }

        if (request.SizeBytes != expectedSizeBytes)
        {
            throw new ScreenshotCaptureContractException(
                $"Raw byte count does not match RGBA8 dimensions. Expected={expectedSizeBytes}, Actual={request.SizeBytes}.");
        }

        if (expectedSizeBytes > IpcScreenshotCaptureLimits.MaximumRawImageBytes)
        {
            throw new ScreenshotCaptureContractException(
                $"Raw byte count exceeds the screenshot limit of {IpcScreenshotCaptureLimits.MaximumRawImageBytes} bytes.");
        }
    }

    private static ScreenshotArtifactPaths ResolvePaths (
        ResolvedUnityProjectContext unityProject,
        string captureId)
    {
        var artifactDirectory = UcliStoragePathResolver.ResolveScreenshotCaptureArtifactsDirectory(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            captureId);
        var stagingDirectory = UcliStoragePathResolver.ResolveScreenshotCaptureStagingDirectory(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            captureId);
        return new ScreenshotArtifactPaths(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            captureId,
            artifactDirectory,
            UcliStoragePathResolver.ResolveScreenshotCaptureArtifactPath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                captureId),
            stagingDirectory,
            UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                captureId));
    }

    private static void EnsureExpectedPathLayout (ScreenshotArtifactPaths paths)
    {
        var expectedArtifactDirectory = UcliStoragePathResolver.ResolveScreenshotCaptureArtifactsDirectory(
            paths.RepositoryRoot,
            paths.ProjectFingerprint,
            paths.CaptureId);
        var expectedPngPath = UcliStoragePathResolver.ResolveScreenshotCaptureArtifactPath(
            paths.RepositoryRoot,
            paths.ProjectFingerprint,
            paths.CaptureId);
        var expectedStagingDirectory = UcliStoragePathResolver.ResolveScreenshotCaptureStagingDirectory(
            paths.RepositoryRoot,
            paths.ProjectFingerprint,
            paths.CaptureId);
        var expectedRawStagingPath = UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
            paths.RepositoryRoot,
            paths.ProjectFingerprint,
            paths.CaptureId);

        EnsureSamePath(paths.ArtifactDirectory, expectedArtifactDirectory, "artifact directory");
        EnsureSamePath(paths.PngPath, expectedPngPath, "PNG artifact");
        EnsureSamePath(paths.StagingDirectory, expectedStagingDirectory, "staging directory");
        EnsureSamePath(paths.RawStagingPath, expectedRawStagingPath, "raw staging file");
    }

    private static void EnsureSamePath (
        string actualPath,
        string expectedPath,
        string pathKind)
    {
        var normalizedActualPath = Path.GetFullPath(actualPath);
        if (!PathIdentity.IsSamePath(normalizedActualPath, expectedPath))
        {
            throw new InvalidOperationException(
                $"Screenshot {pathKind} path must be {expectedPath}: {actualPath}");
        }
    }

    private static string NormalizeRepositoryRelativeArtifactPath (ScreenshotArtifactPaths paths)
    {
        var result = RepositoryPathNormalizer.TryNormalize(paths.RepositoryRoot, paths.PngPath);
        if (!result.IsSuccess || string.Equals(result.RepositoryRelativeSlashPath, ".", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Screenshot artifact path must resolve inside the repository root: {paths.PngPath}");
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

    private static ScreenshotArtifactDiscardResult DiscardCore (ScreenshotArtifactPaths paths)
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

    private static void DeleteStagingLayout (ScreenshotArtifactPaths paths)
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
        ScreenshotArtifactPaths paths,
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

    private readonly record struct CommittedFile (
        long SizeBytes,
        string Digest);
}
