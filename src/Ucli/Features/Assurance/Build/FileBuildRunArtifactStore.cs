using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Assurance.Build;

/// <summary> Creates and writes build run artifacts in local uCLI storage. </summary>
internal sealed class FileBuildRunArtifactStore : IBuildRunArtifactStore
{
    private const int OutputManifestSchemaVersion = 1;

    /// <inheritdoc />
    public ValueTask<BuildRunArtifactPrepareResult> PrepareAsync (
        ResolvedUnityProjectContext unityProject,
        string runId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var runDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                runId);
            var paths = new BuildRunArtifactPaths(
                RunDirectory: runDirectory,
                BuildJsonPath: Path.Combine(runDirectory, UcliStoragePathNames.BuildMetadataFileName),
                BuildReportPath: Path.Combine(runDirectory, UcliStoragePathNames.BuildReportFileName),
                BuildLogPath: Path.Combine(runDirectory, UcliStoragePathNames.BuildLogFileName),
                OutputManifestPath: Path.Combine(runDirectory, UcliStoragePathNames.BuildOutputManifestFileName),
                OutputDirectory: Path.Combine(runDirectory, UcliStoragePathNames.BuildOutputDirectoryName));
            if (File.Exists(paths.RunDirectory) || Directory.Exists(paths.RunDirectory))
            {
                return ValueTask.FromResult(BuildRunArtifactPrepareResult.Failure(ExecutionError.InternalError(
                    $"Build run artifact directory already exists: {paths.RunDirectory}.",
                    BuildErrorCodes.BuildArtifactWriteFailed)));
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(paths.RunDirectory);
            FileSystemAccessBoundary.EnsureSecureDirectory(paths.OutputDirectory);
            return ValueTask.FromResult(BuildRunArtifactPrepareResult.Success(paths));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ValueTask.FromResult(BuildRunArtifactPrepareResult.Failure(ExecutionError.InternalError(
                $"Build artifact path is invalid. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ValueTask.FromResult(BuildRunArtifactPrepareResult.Failure(ExecutionError.InternalError(
                $"Failed to prepare build artifact directory. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed)));
        }
    }

    /// <inheritdoc />
    public async ValueTask<BuildOutputManifestResult> WriteOutputManifestAsync (
        BuildRunArtifactPaths paths,
        string target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var files = await ScanOutputFilesAsync(paths.OutputDirectory, cancellationToken).ConfigureAwait(false);
            files.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
            var totalBytes = files.Sum(static file => file.SizeBytes);
            var manifestContent = new BuildOutputManifestContent(
                SchemaVersion: OutputManifestSchemaVersion,
                OutputRoot: paths.OutputDirectory,
                Target: target,
                FileCount: files.Count,
                TotalBytes: totalBytes,
                Files: files);
            var manifestDigest = CalculateManifestContentDigest(manifestContent);
            var manifest = CreateOutputManifest(manifestContent, manifestDigest);

            await WriteJsonAtomicallyAsync(paths.OutputManifestPath, manifest, cancellationToken).ConfigureAwait(false);
            return BuildOutputManifestResult.Success(manifest);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildOutputManifestResult.Failure(ExecutionError.InternalError(
                $"Build output manifest path is invalid. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestFailed));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            return BuildOutputManifestResult.Failure(ExecutionError.InternalError(
                $"Failed to write build output manifest. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestFailed));
        }
    }

    /// <inheritdoc />
    public async ValueTask<BuildArtifactWriteResult> WriteMetadataAsync (
        BuildRunArtifactPaths paths,
        BuildRunMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(metadata);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await WriteJsonAtomicallyAsync(paths.BuildJsonPath, metadata, cancellationToken).ConfigureAwait(false);
            return await CalculateDigestAsync(paths.BuildJsonPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildArtifactWriteResult.Failure(ExecutionError.InternalError(
                $"Build metadata path is invalid. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            return BuildArtifactWriteResult.Failure(ExecutionError.InternalError(
                $"Failed to write build metadata artifact. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }
    }

    /// <inheritdoc />
    public async ValueTask<BuildArtifactWriteResult> CalculateDigestAsync (
        string path,
        CancellationToken cancellationToken = default)
    {
        return await CalculateDigestCoreAsync(path, BuildErrorCodes.BuildArtifactWriteFailed, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<BuildArtifactWriteResult> CalculateOutputManifestContentDigestAsync (
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            EnsureReadableArtifactPath(path);
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
            var manifest = await JsonSerializer.DeserializeAsync<BuildOutputManifest>(
                    stream,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken)
                .ConfigureAwait(false);
            if (manifest == null)
            {
                throw new JsonException("Build output manifest was empty.");
            }

            var contentDigest = CalculateManifestContentDigest(CreateManifestContent(manifest));
            if (!string.Equals(contentDigest, manifest.ManifestDigest, StringComparison.Ordinal))
            {
                return BuildArtifactWriteResult.Failure(ExecutionError.InternalError(
                    "Build output manifest digest did not match the persisted manifest content.",
                    BuildErrorCodes.BuildOutputDigestMismatch));
            }

            return BuildArtifactWriteResult.Success(contentDigest);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildArtifactWriteResult.Failure(ExecutionError.InternalError(
                $"Build output manifest path is invalid. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestFailed));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return BuildArtifactWriteResult.Failure(ExecutionError.InternalError(
                $"Build output manifest is missing: {path}. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestFailed));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            return BuildArtifactWriteResult.Failure(ExecutionError.InternalError(
                $"Failed to digest build output manifest content: {path}. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestFailed));
        }
    }

    /// <inheritdoc />
    public async ValueTask<BuildArtifactWriteResult> CalculateRequiredDigestAsync (
        string path,
        UcliCode missingCode,
        CancellationToken cancellationToken = default)
    {
        if (!missingCode.IsValid)
        {
            throw new ArgumentException(UcliCode.InvalidValueMessage, nameof(missingCode));
        }

        return await CalculateDigestCoreAsync(path, missingCode, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<BuildArtifactWriteResult> CalculateDigestCoreAsync (
        string path,
        UcliCode missingCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            EnsureReadableArtifactPath(path);
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
            using var sha256 = SHA256.Create();
            var digestBytes = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
            return BuildArtifactWriteResult.Success(Sha256LowerHex.ToLowerHex(digestBytes));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildArtifactWriteResult.Failure(ExecutionError.InternalError(
                $"Build artifact path is invalid. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return BuildArtifactWriteResult.Failure(ExecutionError.InternalError(
                $"Build artifact is missing: {path}. {exception.Message}",
                missingCode));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildArtifactWriteResult.Failure(ExecutionError.InternalError(
                $"Failed to digest build artifact: {path}. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }
    }

    private static async ValueTask<List<BuildOutputManifestFile>> ScanOutputFilesAsync (
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(outputDirectory) && !File.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException($"Build output directory was not found: {outputDirectory}");
        }

        var rootAttributes = File.GetAttributes(outputDirectory);
        if ((rootAttributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build output directory must not be a reparse point: {outputDirectory}");
        }

        if ((rootAttributes & FileAttributes.Directory) == 0)
        {
            throw new IOException($"Build output root must be a directory: {outputDirectory}");
        }

        var files = new List<BuildOutputManifestFile>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(outputDirectory);
        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pendingDirectories.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"Build output manifest does not allow reparse points: {entry}");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pendingDirectories.Push(entry);
                    continue;
                }

                files.Add(await CreateManifestFileAsync(outputDirectory, entry, cancellationToken).ConfigureAwait(false));
            }
        }

        return files;
    }

    private static async ValueTask<BuildOutputManifestFile> CreateManifestFileAsync (
        string outputDirectory,
        string path,
        CancellationToken cancellationToken)
    {
        var relativePath = Path.GetRelativePath(outputDirectory, path).Replace(Path.DirectorySeparatorChar, '/');
        var fileInfo = new FileInfo(path);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        using var sha256 = SHA256.Create();
        var digestBytes = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return new BuildOutputManifestFile(
            Path: relativePath,
            SizeBytes: fileInfo.Length,
            Sha256: Sha256LowerHex.ToLowerHex(digestBytes));
    }

    private static string CalculateManifestContentDigest (BuildOutputManifestContent content)
    {
        return Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(content, IpcJsonSerializerOptions.Default)));
    }

    private static BuildOutputManifest CreateOutputManifest (
        BuildOutputManifestContent content,
        string manifestDigest)
    {
        return new BuildOutputManifest(
            SchemaVersion: content.SchemaVersion,
            OutputRoot: content.OutputRoot,
            Target: content.Target,
            FileCount: content.FileCount,
            TotalBytes: content.TotalBytes,
            Files: content.Files,
            ManifestDigest: manifestDigest);
    }

    private static BuildOutputManifestContent CreateManifestContent (BuildOutputManifest manifest)
    {
        return new BuildOutputManifestContent(
            SchemaVersion: manifest.SchemaVersion,
            OutputRoot: manifest.OutputRoot,
            Target: manifest.Target,
            FileCount: manifest.FileCount,
            TotalBytes: manifest.TotalBytes,
            Files: manifest.Files);
    }

    private static async ValueTask WriteJsonAtomicallyAsync<T> (
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directoryPath = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException($"Artifact directory path could not be resolved: {path}");
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
        var tempPath = path + $".tmp.{Guid.NewGuid():N}";

        try
        {
            EnsureWritableArtifactPath(tempPath);
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        value,
                        IpcJsonSerializerOptions.Default,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            FileSystemAccessBoundary.EnsureSecureFile(tempPath);
            EnsureWritableArtifactPath(path);
            ReplaceFile(tempPath, path);
            FileSystemAccessBoundary.EnsureSecureFile(path);
        }
        finally
        {
            DeleteTemporaryFileIfExists(tempPath);
        }
    }

    private static void EnsureReadableArtifactPath (string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException($"Build artifact was not found: {path}", path);
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build artifact source must not be a reparse point: {path}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"Build artifact source must not be a directory: {path}");
        }
    }

    private static void EnsureWritableArtifactPath (string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build artifact target must not be a reparse point: {path}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"Build artifact target must not be a directory: {path}");
        }
    }

    private static void ReplaceFile (
        string temporaryPath,
        string path)
    {
        try
        {
            File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        catch (FileNotFoundException)
        {
            MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
        }
        catch (IOException) when (!File.Exists(path))
        {
            MoveOrReplaceWhenCreatedConcurrently(temporaryPath, path);
        }
    }

    private static void MoveOrReplaceWhenCreatedConcurrently (
        string temporaryPath,
        string path)
    {
        try
        {
            File.Move(temporaryPath, path);
        }
        catch (IOException) when (File.Exists(path))
        {
            EnsureWritableArtifactPath(path);
            File.Replace(temporaryPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
    }

    private static void DeleteTemporaryFileIfExists (string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record BuildOutputManifestContent (
        int SchemaVersion,
        string OutputRoot,
        string Target,
        int FileCount,
        long TotalBytes,
        IReadOnlyList<BuildOutputManifestFile> Files);
}
