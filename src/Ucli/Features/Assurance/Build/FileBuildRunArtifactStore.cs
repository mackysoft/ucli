using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Cryptography;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Assurance.Build;

/// <summary> Prepares and writes build-run artifacts under local uCLI storage. </summary>
internal sealed class FileBuildRunArtifactStore : IBuildRunArtifactStore
{
    private const int FileStreamBufferSize = 81920;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly BuildOutputManifestJsonContractWriter outputManifestWriter;
    private readonly BuildRunMetadataDocumentWriter metadataWriter;

    /// <summary> Initializes a new instance of the <see cref="FileBuildRunArtifactStore" /> class. </summary>
    public FileBuildRunArtifactStore (
        BuildOutputManifestJsonContractWriter outputManifestWriter,
        BuildRunMetadataDocumentWriter metadataWriter)
    {
        this.outputManifestWriter = outputManifestWriter ?? throw new ArgumentNullException(nameof(outputManifestWriter));
        this.metadataWriter = metadataWriter ?? throw new ArgumentNullException(nameof(metadataWriter));
    }

    /// <inheritdoc />
    public BuildRunArtifactPreparationResult Prepare (
        ResolvedUnityProjectContext unityProject,
        string runId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        BuildRunArtifactPaths paths;
        try
        {
            paths = ResolvePaths(unityProject, runId);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactPreparationResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path is invalid. {exception.Message}"));
        }

        try
        {
            if (File.Exists(paths.ArtifactsDirectory) || Directory.Exists(paths.ArtifactsDirectory))
            {
                return BuildRunArtifactPreparationResult.Failure(ExecutionError.InternalError(
                    $"Build artifact directory already exists: {paths.ArtifactsDirectory}.",
                    BuildErrorCodes.BuildArtifactWriteFailed));
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(paths.ArtifactsDirectory);
            FileSystemAccessBoundary.EnsureSecureDirectory(paths.OutputDirectory);
            return BuildRunArtifactPreparationResult.Success(paths);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactPreparationResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildRunArtifactPreparationResult.Failure(ExecutionError.InternalError(
                $"Failed to prepare build artifact directory. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }
    }

    /// <inheritdoc />
    public async ValueTask<BuildRunArtifactWriteOperationResult> WriteArtifactsAsync (
        BuildRunArtifactWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Paths);
        ArgumentNullException.ThrowIfNull(request.Metadata);
        ArgumentNullException.ThrowIfNull(request.BuildReportJson);
        ArgumentNullException.ThrowIfNull(request.BuildLogText);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetStableName);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            EnsureExpectedPathLayout(request.Paths);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactWriteOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path layout is invalid. {exception.Message}"));
        }
        catch (InvalidOperationException exception)
        {
            return BuildRunArtifactWriteOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path layout is invalid. {exception.Message}"));
        }

        OutputManifestArtifacts outputManifestArtifacts;
        try
        {
            outputManifestArtifacts = await CreateOutputManifestArtifactsAsync(
                    request.Paths,
                    request.TargetStableName,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OutputDigestMismatchException exception)
        {
            return BuildRunArtifactWriteOperationResult.Failure(ExecutionError.InternalError(
                $"Build output digest accounting failed. {exception.Message}",
                BuildErrorCodes.BuildOutputDigestMismatch));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactWriteOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build output manifest path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildRunArtifactWriteOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to generate build output manifest. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestFailed));
        }

        BuildArtifactRef buildReportRef;
        BuildArtifactRef buildLogRef;
        try
        {
            var buildReportDigest = await WriteTextAtomicallyAsync(
                    request.Paths.BuildReportJsonPath,
                    request.BuildReportJson,
                    cancellationToken)
                .ConfigureAwait(false);
            buildReportRef = CreateArtifactRef(
                BuildArtifactKeys.BuildReport,
                BuildArtifactKind.BuildReport,
                request.Paths.RepositoryRoot,
                request.Paths.BuildReportJsonPath,
                buildReportDigest);

            var buildLogDigest = await WriteTextAtomicallyAsync(
                    request.Paths.BuildLogPath,
                    request.BuildLogText,
                    cancellationToken)
                .ConfigureAwait(false);
            buildLogRef = CreateArtifactRef(
                BuildArtifactKeys.BuildLog,
                BuildArtifactKind.BuildLog,
                request.Paths.RepositoryRoot,
                request.Paths.BuildLogPath,
                buildLogDigest);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactWriteOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildRunArtifactWriteOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to write build artifacts. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }

        BuildArtifactRef outputManifestRef;
        try
        {
            var outputManifestJson = outputManifestWriter.Write(outputManifestArtifacts.Contract);
            await WriteTextAtomicallyAsync(
                    request.Paths.OutputManifestJsonPath,
                    outputManifestJson,
                    cancellationToken)
                .ConfigureAwait(false);
            outputManifestRef = CreateArtifactRef(
                BuildArtifactKeys.BuildOutputManifest,
                BuildArtifactKind.BuildOutputManifest,
                request.Paths.RepositoryRoot,
                request.Paths.OutputManifestJsonPath,
                outputManifestArtifacts.Summary.ManifestDigest);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactWriteOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build output manifest path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildRunArtifactWriteOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to write build output manifest. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestFailed));
        }

        BuildArtifactRef buildRef;
        try
        {
            var buildJson = metadataWriter.Write(
                request.Metadata,
                [
                    buildReportRef,
                    outputManifestRef,
                    buildLogRef,
                ]);
            var buildDigest = await WriteTextAtomicallyAsync(
                    request.Paths.BuildJsonPath,
                    buildJson,
                    cancellationToken)
                .ConfigureAwait(false);
            buildRef = CreateArtifactRef(
                BuildArtifactKeys.Build,
                BuildArtifactKind.Build,
                request.Paths.RepositoryRoot,
                request.Paths.BuildJsonPath,
                buildDigest);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactWriteOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build metadata path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildRunArtifactWriteOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to write build metadata. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }

        return BuildRunArtifactWriteOperationResult.Success(new BuildRunArtifactWriteResult(
            buildRef,
            buildReportRef,
            outputManifestRef,
            buildLogRef,
            outputManifestArtifacts.Summary));
    }

    private static BuildRunArtifactPaths ResolvePaths (
        ResolvedUnityProjectContext unityProject,
        string runId)
    {
        var artifactsDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            runId);

        return new BuildRunArtifactPaths(
            unityProject.RepositoryRoot,
            runId,
            artifactsDirectory,
            Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildMetadataFileName),
            Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildReportFileName),
            Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildLogFileName),
            Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildOutputManifestFileName),
            Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildOutputDirectoryName));
    }

    private static void EnsureExpectedPathLayout (BuildRunArtifactPaths paths)
    {
        var artifactsDirectory = Path.GetFullPath(paths.ArtifactsDirectory);
        if (!string.Equals(artifactsDirectory, paths.ArtifactsDirectory, GetPathComparison()))
        {
            throw new InvalidOperationException($"Artifact directory must be normalized: {paths.ArtifactsDirectory}");
        }

        EnsureExpectedArtifactsDirectory(paths, artifactsDirectory);
        EnsureExpectedPath(
            artifactsDirectory,
            paths.BuildJsonPath,
            UcliStoragePathNames.BuildMetadataFileName);
        EnsureExpectedPath(
            artifactsDirectory,
            paths.BuildReportJsonPath,
            UcliStoragePathNames.BuildReportFileName);
        EnsureExpectedPath(
            artifactsDirectory,
            paths.BuildLogPath,
            UcliStoragePathNames.BuildLogFileName);
        EnsureExpectedPath(
            artifactsDirectory,
            paths.OutputManifestJsonPath,
            UcliStoragePathNames.BuildOutputManifestFileName);
        EnsureExpectedPath(
            artifactsDirectory,
            paths.OutputDirectory,
            UcliStoragePathNames.BuildOutputDirectoryName);
    }

    private static void EnsureExpectedArtifactsDirectory (
        BuildRunArtifactPaths paths,
        string artifactsDirectory)
    {
        var relativePath = NormalizeRepositoryRelativePath(paths.RepositoryRoot, artifactsDirectory);
        var segments = relativePath.Split('/');
        if (segments.Length != 7
            || !string.Equals(segments[0], UcliStoragePathNames.UcliDirectoryName, StringComparison.Ordinal)
            || !string.Equals(segments[1], UcliStoragePathNames.LocalDirectoryName, StringComparison.Ordinal)
            || !string.Equals(segments[2], UcliStoragePathNames.FingerprintsDirectoryName, StringComparison.Ordinal)
            || !string.Equals(segments[4], UcliStoragePathNames.ArtifactsDirectoryName, StringComparison.Ordinal)
            || !string.Equals(segments[5], UcliStoragePathNames.BuildArtifactsDirectoryName, StringComparison.Ordinal)
            || !string.Equals(segments[6], paths.RunId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Artifact directory must use the build-run storage layout: {paths.ArtifactsDirectory}");
        }
    }

    private static void EnsureExpectedPath (
        string artifactsDirectory,
        string actualPath,
        string expectedFileName)
    {
        var expectedPath = Path.Combine(artifactsDirectory, expectedFileName);
        var normalizedActualPath = Path.GetFullPath(actualPath);
        if (!string.Equals(normalizedActualPath, expectedPath, GetPathComparison()))
        {
            throw new InvalidOperationException(
                $"Artifact path must be {expectedPath}: {actualPath}");
        }
    }

    private async ValueTask<OutputManifestArtifacts> CreateOutputManifestArtifactsAsync (
        BuildRunArtifactPaths paths,
        string targetStableName,
        CancellationToken cancellationToken)
    {
        var outputRoot = NormalizeRepositoryRelativePath(paths.RepositoryRoot, paths.OutputDirectory);
        var candidates = EnumerateOutputFileCandidates(paths.OutputDirectory);
        candidates.Sort(static (left, right) => string.CompareOrdinal(left.RelativePath, right.RelativePath));

        var files = new List<BuildOutputManifestFileJsonContract>(candidates.Count);
        long totalBytes = 0;
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var fileInfo = new FileInfo(candidate.FullPath);
            var sizeBytes = fileInfo.Length;
            var sha256 = await ComputeFileSha256Async(
                    candidate.FullPath,
                    sizeBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            totalBytes += sizeBytes;
            files.Add(new BuildOutputManifestFileJsonContract(
                candidate.RelativePath,
                sizeBytes,
                sha256));
        }

        var content = new BuildOutputManifestContentJsonContract(
            BuildOutputManifestJsonContract.CurrentSchemaVersion,
            outputRoot,
            targetStableName,
            files.Count,
            totalBytes,
            files);
        var manifestDigest = outputManifestWriter.CalculateManifestDigest(content);
        var contract = new BuildOutputManifestJsonContract(
            content.SchemaVersion,
            content.OutputRoot,
            content.Target,
            content.FileCount,
            content.TotalBytes,
            content.Files,
            manifestDigest);

        return new OutputManifestArtifacts(
            contract,
            new BuildOutputManifestSummary(
                manifestDigest,
                files.Count,
                totalBytes));
    }

    private static List<OutputFileCandidate> EnumerateOutputFileCandidates (string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException($"Build output directory was not found: {outputDirectory}");
        }

        var outputRootFullPath = Path.GetFullPath(outputDirectory);
        EnsureOutputDirectoryNode(outputRootFullPath);

        var files = new List<OutputFileCandidate>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(outputRootFullPath);
        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            foreach (var entryPath in Directory.EnumerateFileSystemEntries(currentDirectory))
            {
                var attributes = File.GetAttributes(entryPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"Build output entry must not be a reparse point: {entryPath}");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pendingDirectories.Push(Path.GetFullPath(entryPath));
                    continue;
                }

                var fullPath = Path.GetFullPath(entryPath);
                var platformRelativePath = Path.GetRelativePath(outputRootFullPath, fullPath);
                EnsureSafeOutputRelativePath(platformRelativePath, fullPath);
                var relativePath = PathStringNormalizer.ToSlashSeparated(platformRelativePath);
                files.Add(new OutputFileCandidate(fullPath, relativePath));
            }
        }

        return files;
    }

    private static void EnsureOutputDirectoryNode (string outputDirectory)
    {
        var attributes = File.GetAttributes(outputDirectory);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build output directory must not be a reparse point: {outputDirectory}");
        }

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException($"Build output path must be a directory: {outputDirectory}");
        }
    }

    private static void EnsureSafeOutputRelativePath (
        string platformRelativePath,
        string fullPath)
    {
        if (string.IsNullOrWhiteSpace(platformRelativePath)
            || ContainsLiteralBackslash(platformRelativePath)
            || Path.IsPathRooted(platformRelativePath)
            || LooksLikeWindowsRootedPath(platformRelativePath))
        {
            throw new IOException($"Build output file path escaped the output directory: {fullPath}");
        }

        var relativePath = PathStringNormalizer.ToSlashSeparated(platformRelativePath);
        foreach (var segment in relativePath.Split('/'))
        {
            if (string.IsNullOrEmpty(segment)
                || string.Equals(segment, ".", StringComparison.Ordinal)
                || string.Equals(segment, "..", StringComparison.Ordinal))
            {
                throw new IOException($"Build output file path escaped the output directory: {fullPath}");
            }
        }
    }

    private static bool ContainsLiteralBackslash (string path)
    {
        return Path.DirectorySeparatorChar != '\\'
            && path.Contains('\\', StringComparison.Ordinal);
    }

    private static bool LooksLikeWindowsRootedPath (string path)
    {
        return path.Length >= 2
            && path[1] == ':'
            && char.IsAsciiLetter(path[0]);
    }

    private static async ValueTask<string> ComputeFileSha256Async (
        string filePath,
        long expectedLength,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureReadableOutputFile(filePath);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(FileStreamBufferSize);
        long totalBytesRead = 0;
        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileStreamBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
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

        if (totalBytesRead != expectedLength)
        {
            throw new OutputDigestMismatchException(
                $"Build output file length changed while hashing: {filePath}.");
        }

        var finalLength = new FileInfo(filePath).Length;
        if (finalLength != expectedLength)
        {
            throw new OutputDigestMismatchException(
                $"Build output file length changed after hashing: {filePath}.");
        }

        return Sha256LowerHex.GetHashAndReset(hash);
    }

    private static void EnsureReadableOutputFile (string filePath)
    {
        if (!File.Exists(filePath) && !Directory.Exists(filePath))
        {
            throw new FileNotFoundException($"Build output file was not found: {filePath}", filePath);
        }

        var attributes = File.GetAttributes(filePath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build output file must not be a reparse point: {filePath}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"Build output file must not be a directory: {filePath}");
        }

        if (!FileSystemNodeClassifier.IsRegularFile(filePath, attributes))
        {
            throw new IOException($"Build output file must be a regular file: {filePath}");
        }
    }

    private static async ValueTask<string> WriteTextAtomicallyAsync (
        string path,
        string text,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directoryPath = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException($"Artifact directory path could not be resolved: {path}");
        }

        var digest = ComputeUtf8Sha256(text);
        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
        var tempPath = path + $".tmp.{Guid.NewGuid():N}";

        try
        {
            EnsureWritableArtifactPath(tempPath);
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                FileStreamBufferSize,
                FileOptions.Asynchronous))
            using (var writer = new StreamWriter(stream, Utf8NoBom))
            {
                await writer
                    .WriteAsync(text.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
            }

            FileSystemAccessBoundary.EnsureSecureFile(tempPath);
            EnsureWritableArtifactPath(path);
            ReplaceFile(tempPath, path);
            FileSystemAccessBoundary.EnsureSecureFile(path);
            return digest;
        }
        finally
        {
            DeleteTemporaryFileIfExists(tempPath);
        }
    }

    private static string ComputeUtf8Sha256 (string text)
    {
        using var hashWriter = new Utf8Sha256HashWriter();
        hashWriter.Append(text);
        return hashWriter.GetHashAndReset();
    }

    private static BuildArtifactRef CreateArtifactRef (
        string key,
        BuildArtifactKind kind,
        string repositoryRoot,
        string path,
        string sha256)
    {
        return new BuildArtifactRef(
            key,
            kind,
            NormalizeRepositoryRelativePath(repositoryRoot, path),
            sha256);
    }

    private static string NormalizeRepositoryRelativePath (
        string repositoryRoot,
        string path)
    {
        var result = RepositoryPathNormalizer.TryNormalize(repositoryRoot, path);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.DiagnosticMessage);
        }

        return result.RepositoryRelativeSlashPath!;
    }

    private static StringComparison GetPathComparison ()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
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

    private sealed record OutputFileCandidate (
        string FullPath,
        string RelativePath);

    private sealed record OutputManifestArtifacts (
        BuildOutputManifestJsonContract Contract,
        BuildOutputManifestSummary Summary);

    private sealed class OutputDigestMismatchException : Exception
    {
        public OutputDigestMismatchException (string message)
            : base(message)
        {
        }
    }

}
