using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Cryptography;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Assurance.Build;

/// <summary> Prepares and writes build-run artifacts under local uCLI storage. </summary>
internal sealed class FileBuildRunArtifactStore : IBuildRunArtifactStore
{
    private const int FileStreamBufferSize = 81920;
    private const string OutputEntryIdPrefix = "output-";
    private const int PosixFileStatusBufferSize = 256;
    private const int PosixFileTypeMask = 0xF000;
    private const int PosixRegularFileType = 0x8000;
    private const int LinuxFileModeOffset = 24;
    private const int LinuxArm64FileModeOffset = 16;
    private const int LinuxDeviceOffset = 0;
    private const int LinuxInodeOffset = 8;
    private const int MacOsDeviceOffset = 0;
    private const int MacOsFileModeOffset = 4;
    private const int MacOsInodeOffset = 8;

    private static readonly string OutputEntryKindDirectory = ContractLiteralCodec.ToValue(BuildOutputManifestEntryKind.Directory);
    private static readonly string OutputEntryKindFile = ContractLiteralCodec.ToValue(BuildOutputManifestEntryKind.File);
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

            if (File.Exists(paths.RunnerOutputDirectory) || Directory.Exists(paths.RunnerOutputDirectory))
            {
                return BuildRunArtifactPreparationResult.Failure(ExecutionError.InternalError(
                    $"Build runner output directory already exists: {paths.RunnerOutputDirectory}.",
                    BuildErrorCodes.BuildArtifactWriteFailed));
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(paths.ArtifactsDirectory);
            FileSystemAccessBoundary.EnsureSecureDirectory(paths.RunnerOutputDirectory);
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
    public BuildRunArtifactPreparationResult PrepareBuildPipelineOutputLayout (
        BuildRunArtifactPaths paths,
        string buildTarget,
        IpcBuildOutputLayout outputLayout)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildTarget);
        ArgumentNullException.ThrowIfNull(outputLayout);

        try
        {
            EnsureExpectedPathLayout(paths);
            EnsureExpectedBuildPipelineOutputLayout(paths, buildTarget, outputLayout);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactPreparationResult.Failure(ExecutionError.InvalidArgument(
                $"BuildPipeline output layout path is invalid. {exception.Message}"));
        }
        catch (InvalidOperationException exception)
        {
            return BuildRunArtifactPreparationResult.Failure(ExecutionError.InvalidArgument(
                $"BuildPipeline output layout is invalid. {exception.Message}",
                BuildErrorCodes.BuildInputsInvalid));
        }

        try
        {
            var parentDirectory = Path.GetDirectoryName(outputLayout.LocationPathName);
            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                throw new InvalidOperationException(
                    $"BuildPipeline output parent directory could not be resolved: {outputLayout.LocationPathName}");
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(parentDirectory);
            EnsureBuildPipelineOutputTargetDoesNotExist(outputLayout.LocationPathName);
            return BuildRunArtifactPreparationResult.Success(paths);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactPreparationResult.Failure(ExecutionError.InvalidArgument(
                $"BuildPipeline output layout path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildRunArtifactPreparationResult.Failure(ExecutionError.InternalError(
                $"Failed to prepare BuildPipeline output layout. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }
    }

    /// <inheritdoc />
    public async ValueTask<BuildRunArtifactAccountingOperationResult> AccountArtifactsAsync (
        BuildRunArtifactAccountingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BuildTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UnityBuildTarget);
        ArgumentNullException.ThrowIfNull(request.OutputSources);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            EnsureExpectedPathLayout(request.Paths);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path layout is invalid. {exception.Message}"));
        }
        catch (InvalidOperationException exception)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path layout is invalid. {exception.Message}"));
        }

        OutputManifestArtifacts outputManifestArtifacts;
        try
        {
            outputManifestArtifacts = await CreateOutputManifestArtifactsAsync(
                    request.Paths,
                    request.BuildTarget,
                    request.UnityBuildTarget,
                    request.OutputSources,
                    request.AllowEmptyOutputManifest,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OutputPathPolicyException exception)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build output path is invalid. {exception.Message}",
                BuildErrorCodes.BuildOutputPathInvalid));
        }
        catch (OutputManifestDigestMismatchException exception)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InternalError(
                $"Build output manifest digest mismatch. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestDigestMismatch));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build output path is invalid. {exception.Message}",
                BuildErrorCodes.BuildOutputPathInvalid));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to generate build output manifest. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestFailed));
        }

        var buildReportAccountingResult = await AccountExistingArtifactAsync(
                BuildArtifactKind.BuildReport,
                request.Paths.ArtifactsDirectory,
                request.Paths.BuildReportJsonPath,
                "BuildReport artifact",
                BuildErrorCodes.BuildReportMissing,
                cancellationToken)
            .ConfigureAwait(false);
        if (!buildReportAccountingResult.IsSuccess)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(buildReportAccountingResult.Error!);
        }

        var buildLogAccountingResult = await AccountExistingArtifactAsync(
                BuildArtifactKind.BuildLog,
                request.Paths.ArtifactsDirectory,
                request.Paths.BuildLogPath,
                "Build log artifact",
                BuildErrorCodes.BuildArtifactWriteFailed,
                cancellationToken)
            .ConfigureAwait(false);
        if (!buildLogAccountingResult.IsSuccess)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(buildLogAccountingResult.Error!);
        }

        var buildReportRef = buildReportAccountingResult.Artifact!;
        var buildLogRef = buildLogAccountingResult.Artifact!;

        BuildArtifactRef outputManifestRef;
        try
        {
            VerifyManifestDigest(outputManifestArtifacts.Contract);
            var outputManifestJson = outputManifestWriter.Write(outputManifestArtifacts.Contract);
            var outputManifestDigest = await WriteTextAtomicallyAsync(
                    request.Paths.OutputManifestJsonPath,
                    outputManifestJson,
                    cancellationToken)
                .ConfigureAwait(false);
            var persistedOutputManifestDigest = await ComputeExistingArtifactSha256Async(
                    request.Paths.OutputManifestJsonPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(outputManifestDigest, persistedOutputManifestDigest, StringComparison.Ordinal))
            {
                throw new OutputManifestArtifactDigestMismatchException(
                    $"Expected={outputManifestDigest}, Actual={persistedOutputManifestDigest}.");
            }

            outputManifestRef = CreateArtifactRef(
                BuildArtifactKind.BuildOutputManifest,
                request.Paths.ArtifactsDirectory,
                request.Paths.OutputManifestJsonPath,
                persistedOutputManifestDigest);
        }
        catch (OutputManifestDigestMismatchException exception)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InternalError(
                $"Build output manifest digest mismatch. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestDigestMismatch));
        }
        catch (OutputManifestArtifactDigestMismatchException exception)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InternalError(
                $"Build output manifest artifact digest mismatch. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestArtifactDigestMismatch));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build output manifest path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to write build output manifest. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestFailed));
        }

        return BuildRunArtifactAccountingOperationResult.Success(new BuildRunArtifactAccountingResult(
            buildReportRef,
            outputManifestRef,
            buildLogRef,
            outputManifestArtifacts.Summary));
    }

    /// <inheritdoc />
    public async ValueTask<BuildArtifactRefWriteResult> WriteMetadataAsync (
        BuildRunMetadataWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Paths);
        ArgumentNullException.ThrowIfNull(request.Metadata);
        ArgumentNullException.ThrowIfNull(request.Accounting);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            EnsureExpectedPathLayout(request.Paths);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildArtifactRefWriteResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path layout is invalid. {exception.Message}"));
        }
        catch (InvalidOperationException exception)
        {
            return BuildArtifactRefWriteResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path layout is invalid. {exception.Message}"));
        }

        BuildArtifactRef buildRef;
        try
        {
            var buildJson = metadataWriter.Write(
                request.Metadata,
                [
                    request.Accounting.BuildReport,
                    request.Accounting.BuildOutputManifest,
                    request.Accounting.BuildLog,
                ]);
            var buildDigest = await WriteTextAtomicallyAsync(
                    request.Paths.BuildJsonPath,
                    buildJson,
                    cancellationToken)
                .ConfigureAwait(false);
            buildRef = CreateArtifactRef(
                BuildArtifactKind.Build,
                request.Paths.ArtifactsDirectory,
                request.Paths.BuildJsonPath,
                buildDigest);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildArtifactRefWriteResult.Failure(ExecutionError.InvalidArgument(
                $"Build metadata path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildArtifactRefWriteResult.Failure(ExecutionError.InternalError(
                $"Failed to write build metadata. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }

        return BuildArtifactRefWriteResult.Success(buildRef);
    }

    private static BuildRunArtifactPaths ResolvePaths (
        ResolvedUnityProjectContext unityProject,
        string runId)
    {
        var artifactsDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint,
            runId);
        var runnerOutputDirectory = UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
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
            runnerOutputDirectory,
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
            paths.ArtifactOutputDirectory,
            UcliStoragePathNames.BuildOutputDirectoryName);
        EnsureExpectedRunnerOutputDirectory(paths);
        EnsureSeparatedOutputRoots(paths);
    }

    private static void EnsureExpectedBuildPipelineOutputLayout (
        BuildRunArtifactPaths paths,
        string buildTarget,
        IpcBuildOutputLayout outputLayout)
    {
        if (!IpcBuildOutputLayoutResolver.TryResolve(paths.RunnerOutputDirectory, buildTarget, out var expectedLayout))
        {
            throw new InvalidOperationException($"Build target does not have a deterministic BuildPipeline output layout: {buildTarget}");
        }

        if (!string.Equals(outputLayout.Shape, expectedLayout!.Shape, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"BuildPipeline output layout shape must be {expectedLayout.Shape}: {outputLayout.Shape}");
        }

        var actualLocationPathName = Path.GetFullPath(outputLayout.LocationPathName);
        var expectedLocationPathName = Path.GetFullPath(expectedLayout.LocationPathName);
        if (!string.Equals(actualLocationPathName, expectedLocationPathName, GetPathComparison()))
        {
            throw new InvalidOperationException(
                $"BuildPipeline output locationPathName must be {expectedLocationPathName}: {outputLayout.LocationPathName}");
        }
    }

    private static void EnsureBuildPipelineOutputTargetDoesNotExist (string locationPathName)
    {
        if (!File.Exists(locationPathName) && !Directory.Exists(locationPathName))
        {
            return;
        }

        var attributes = File.GetAttributes(locationPathName);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"BuildPipeline output target must not be a reparse point: {locationPathName}");
        }

        throw new IOException($"BuildPipeline output target already exists: {locationPathName}");
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

    private static void EnsureExpectedRunnerOutputDirectory (BuildRunArtifactPaths paths)
    {
        var expectedRunnerOutputDirectory = UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
            paths.RepositoryRoot,
            ResolveProjectFingerprintFromArtifactsDirectory(paths),
            paths.RunId);
        var normalizedActualPath = Path.GetFullPath(paths.RunnerOutputDirectory);
        if (!string.Equals(normalizedActualPath, expectedRunnerOutputDirectory, GetPathComparison()))
        {
            throw new InvalidOperationException(
                $"Runner output directory must be {expectedRunnerOutputDirectory}: {paths.RunnerOutputDirectory}");
        }
    }

    private static string ResolveProjectFingerprintFromArtifactsDirectory (BuildRunArtifactPaths paths)
    {
        var relativePath = NormalizeRepositoryRelativePath(paths.RepositoryRoot, paths.ArtifactsDirectory);
        var segments = relativePath.Split('/');
        if (segments.Length < 4)
        {
            throw new InvalidOperationException(
                $"Artifact directory must include a project fingerprint segment: {paths.ArtifactsDirectory}");
        }

        return segments[3];
    }

    private static void EnsureSeparatedOutputRoots (BuildRunArtifactPaths paths)
    {
        var artifactOutputDirectory = Path.GetFullPath(paths.ArtifactOutputDirectory);
        var runnerOutputDirectory = Path.GetFullPath(paths.RunnerOutputDirectory);
        if (PathsAreSameOrNested(artifactOutputDirectory, runnerOutputDirectory)
            || PathsAreSameOrNested(runnerOutputDirectory, artifactOutputDirectory))
        {
            throw new InvalidOperationException(
                "Runner output directory and artifact output directory must be separate non-nested paths.");
        }
    }

    private static bool PathsAreSameOrNested (
        string ancestorPath,
        string candidatePath)
    {
        var relativePath = Path.GetRelativePath(ancestorPath, candidatePath);
        return string.Equals(relativePath, ".", StringComparison.Ordinal)
            || (!Path.IsPathRooted(relativePath) && !IsParentRelativePath(relativePath));
    }

    private static bool IsParentRelativePath (string relativePath)
    {
        return relativePath.Length == 2
            ? relativePath[0] == '.' && relativePath[1] == '.'
            : relativePath.Length > 2
                && relativePath[0] == '.'
                && relativePath[1] == '.'
                && (relativePath[2] == Path.DirectorySeparatorChar || relativePath[2] == Path.AltDirectorySeparatorChar);
    }

    private async ValueTask<OutputManifestArtifacts> CreateOutputManifestArtifactsAsync (
        BuildRunArtifactPaths paths,
        string buildTarget,
        string unityBuildTarget,
        IReadOnlyList<BuildOutputSourceEntry> outputSources,
        bool allowEmptyOutputManifest,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceEntries = ResolveOutputSourceEntries(
            paths,
            outputSources,
            allowEmptyOutputManifest);

        FileSystemAccessBoundary.EnsureSecureDirectory(paths.ArtifactOutputDirectory);
        var entries = new List<BuildOutputManifestEntryJsonContract>(sourceEntries.Count);
        var files = new List<BuildOutputManifestFileJsonContract>();
        long totalBytes = 0;
        for (var i = 0; i < sourceEntries.Count; i++)
        {
            var sourceEntry = sourceEntries[i];
            var entryId = FormatOutputEntryId(i + 1);
            entries.Add(new BuildOutputManifestEntryJsonContract(
                entryId,
                sourceEntry.Kind,
                sourceEntry.SourcePath));

            totalBytes += await IngestOutputSourceEntryAsync(
                    paths,
                    sourceEntry,
                    entryId,
                    files,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        files.Sort(static (left, right) => string.CompareOrdinal(left.LogicalPath, right.LogicalPath));
        EnsureUniqueLogicalPaths(files);

        var content = new BuildOutputManifestContentJsonContract(
            BuildOutputManifestJsonContract.CurrentSchemaVersion,
            new BuildOutputManifestTargetJsonContract(buildTarget, unityBuildTarget),
            entries,
            entries.Count,
            files.Count,
            totalBytes,
            files);
        var manifestDigest = outputManifestWriter.CalculateManifestDigest(content);
        var contract = new BuildOutputManifestJsonContract(
            content.SchemaVersion,
            content.Target,
            content.Entries,
            content.EntryCount,
            content.FileCount,
            content.TotalBytes,
            content.Files,
            manifestDigest);

        return new OutputManifestArtifacts(
            contract,
            new BuildOutputManifestSummary(
                manifestDigest,
                entries.Count,
                files.Count,
                totalBytes));
    }

    private static List<ResolvedOutputSourceEntry> ResolveOutputSourceEntries (
        BuildRunArtifactPaths paths,
        IReadOnlyList<BuildOutputSourceEntry> outputSources,
        bool allowEmptyOutputManifest)
    {
        if (outputSources.Count == 0)
        {
            if (allowEmptyOutputManifest)
            {
                return [];
            }

            throw new InvalidOperationException("Successful build output accounting requires at least one output source entry.");
        }

        var entries = new List<ResolvedOutputSourceEntry>(outputSources.Count);
        var missingSourcePath = string.Empty;
        for (var i = 0; i < outputSources.Count; i++)
        {
            var outputSource = outputSources[i];
            if (outputSource == null || string.IsNullOrWhiteSpace(outputSource.SourcePath))
            {
                throw new OutputPathPolicyException("Output source path must not be empty.");
            }

            if (!Path.IsPathFullyQualified(outputSource.SourcePath))
            {
                throw new OutputPathPolicyException($"Output source path must be fully qualified: {outputSource.SourcePath}");
            }

            var sourcePath = Path.GetFullPath(outputSource.SourcePath);
            EnsureOutputSourceOutsideArtifactRoot(paths, sourcePath);
            EnsureOutputSourceInsideRunnerOutputRoot(paths, sourcePath);
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                missingSourcePath = sourcePath;
                continue;
            }

            EnsureOutputSourcePathHasNoReparsePoint(paths.RunnerOutputDirectory, sourcePath);
            var kind = ResolveOutputSourceEntryKind(sourcePath);
            entries.Add(new ResolvedOutputSourceEntry(sourcePath, kind));
        }

        if (entries.Count == 0 && !string.IsNullOrEmpty(missingSourcePath) && allowEmptyOutputManifest)
        {
            return [];
        }

        if (!string.IsNullOrEmpty(missingSourcePath))
        {
            throw new FileNotFoundException($"Build output source entry was not found: {missingSourcePath}", missingSourcePath);
        }

        if (entries.Count == 0)
        {
            throw new InvalidOperationException("Successful build output accounting requires at least one existing output source entry.");
        }

        return entries;
    }

    private static void EnsureOutputSourceInsideRunnerOutputRoot (
        BuildRunArtifactPaths paths,
        string sourcePath)
    {
        var runnerOutputRoot = Path.GetFullPath(paths.RunnerOutputDirectory);
        if (!PathsAreSameOrNested(runnerOutputRoot, sourcePath))
        {
            throw new OutputPathPolicyException(
                $"Output source path must resolve inside the runner output root. Source={sourcePath}, RunnerOutputRoot={runnerOutputRoot}.");
        }
    }

    private static void EnsureOutputSourceOutsideArtifactRoot (
        BuildRunArtifactPaths paths,
        string sourcePath)
    {
        var artifactRoot = Path.GetFullPath(paths.ArtifactsDirectory);
        if (PathsAreSameOrNested(artifactRoot, sourcePath))
        {
            throw new OutputPathPolicyException(
                $"Output source path must not resolve inside the artifact root. Source={sourcePath}, ArtifactRoot={artifactRoot}.");
        }
    }

    private static void EnsureOutputSourcePathHasNoReparsePoint (
        string runnerOutputRoot,
        string sourcePath)
    {
        var rootPath = Path.GetFullPath(runnerOutputRoot);
        var relativePath = Path.GetRelativePath(rootPath, sourcePath);
        if (string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            EnsureOutputPathNodeIsNotReparsePoint(sourcePath);
            return;
        }

        var currentPath = rootPath;
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            currentPath = Path.Combine(currentPath, segments[i]);
            EnsureOutputPathNodeIsNotReparsePoint(currentPath);
        }
    }

    private static void EnsureOutputPathNodeIsNotReparsePoint (string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build output path must not contain a reparse point: {path}");
        }
    }

    private static string ResolveOutputSourceEntryKind (string sourcePath)
    {
        var attributes = File.GetAttributes(sourcePath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build output source entry must not be a reparse point: {sourcePath}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            return OutputEntryKindDirectory;
        }

        if (!FileSystemNodeClassifier.IsRegularFile(sourcePath, attributes))
        {
            throw new IOException($"Build output source entry must be a regular file or directory: {sourcePath}");
        }

        return OutputEntryKindFile;
    }

    private async ValueTask<long> IngestOutputSourceEntryAsync (
        BuildRunArtifactPaths paths,
        ResolvedOutputSourceEntry sourceEntry,
        string entryId,
        List<BuildOutputManifestFileJsonContract> files,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entryOutputDirectory = Path.Combine(paths.ArtifactOutputDirectory, entryId);
        FileSystemAccessBoundary.EnsureSecureDirectory(entryOutputDirectory);

        if (string.Equals(sourceEntry.Kind, OutputEntryKindFile, StringComparison.Ordinal))
        {
            var fileName = Path.GetFileName(sourceEntry.SourcePath);
            EnsureSafeOutputRelativePath(fileName, sourceEntry.SourcePath);
            var artifactFilePath = Path.Combine(entryOutputDirectory, fileName);
            await CopyRegularFileAsync(sourceEntry.SourcePath, artifactFilePath, cancellationToken).ConfigureAwait(false);
            return await AddArtifactFileEntryAsync(
                    entryId,
                    fileName,
                    sourceEntry.SourcePath,
                    artifactFilePath,
                    files,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var candidates = EnumerateOutputSourceFileCandidates(sourceEntry.SourcePath);
        candidates.Sort(static (left, right) => string.CompareOrdinal(left.RelativePath, right.RelativePath));

        long totalBytes = 0;
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var artifactFilePath = CombineSlashRelativePath(entryOutputDirectory, candidate.RelativePath);
            await CopyRegularFileAsync(candidate.FullPath, artifactFilePath, cancellationToken).ConfigureAwait(false);
            totalBytes += await AddArtifactFileEntryAsync(
                    entryId,
                    candidate.RelativePath,
                    candidate.FullPath,
                    artifactFilePath,
                    files,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return totalBytes;
    }

    private static List<OutputSourceFileCandidate> EnumerateOutputSourceFileCandidates (string sourceDirectory)
    {
        var sourceRootFullPath = Path.GetFullPath(sourceDirectory);
        EnsureOutputDirectoryNode(sourceRootFullPath);

        var files = new List<OutputSourceFileCandidate>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(sourceRootFullPath);
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
                if (!FileSystemNodeClassifier.IsRegularFile(fullPath, attributes))
                {
                    throw new IOException($"Build output file must be a regular file: {fullPath}");
                }

                var platformRelativePath = Path.GetRelativePath(sourceRootFullPath, fullPath);
                EnsureSafeOutputRelativePath(platformRelativePath, fullPath);
                var relativePath = PathStringNormalizer.ToSlashSeparated(platformRelativePath);
                files.Add(new OutputSourceFileCandidate(fullPath, relativePath));
            }
        }

        return files;
    }

    private static async ValueTask CopyRegularFileAsync (
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureReadableOutputFile(sourcePath);
        var sourceIdentity = CaptureOutputFileIdentity(sourcePath);

        var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
        {
            throw new InvalidOperationException($"Artifact output directory path could not be resolved: {destinationPath}");
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(destinationDirectoryPath);
        EnsureWritableArtifactPath(destinationPath);
        var buffer = ArrayPool<byte>.Shared.Rent(FileStreamBufferSize);
        try
        {
            using var sourceStream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileStreamBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            EnsureOpenedOutputFileMatchesIdentity(sourcePath, sourceStream, sourceIdentity);

            using var destinationStream = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                FileStreamBufferSize,
                FileOptions.Asynchronous);
            while (true)
            {
                var bytesRead = await sourceStream
                    .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                await destinationStream
                    .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        FileSystemAccessBoundary.EnsureSecureFile(destinationPath);
    }

    private static OutputFileIdentity CaptureOutputFileIdentity (string sourcePath)
    {
        if (!CanCapturePosixFileIdentity())
        {
            return OutputFileIdentity.Unavailable;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(PosixFileStatusBufferSize);
        try
        {
            if (LStat(sourcePath, buffer) != 0)
            {
                throw new IOException($"Build output file identity could not be inspected: {sourcePath}. errno={Marshal.GetLastWin32Error()}");
            }

            return ReadOutputFileIdentity(buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void EnsureOpenedOutputFileMatchesIdentity (
        string sourcePath,
        FileStream sourceStream,
        OutputFileIdentity expectedIdentity)
    {
        if (!expectedIdentity.IsAvailable)
        {
            EnsureReadableOutputFile(sourcePath);
            return;
        }

        var actualIdentity = CaptureOpenedOutputFileIdentity(sourcePath, sourceStream);
        if (!actualIdentity.IsRegularFile)
        {
            throw new IOException($"Build output file must be a regular file after opening: {sourcePath}");
        }

        if (actualIdentity.Device != expectedIdentity.Device || actualIdentity.Inode != expectedIdentity.Inode)
        {
            throw new IOException($"Build output file changed before it could be copied: {sourcePath}");
        }
    }

    private static OutputFileIdentity CaptureOpenedOutputFileIdentity (
        string sourcePath,
        FileStream sourceStream)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(PosixFileStatusBufferSize);
        try
        {
            if (FStat(sourceStream.SafeFileHandle.DangerousGetHandle(), buffer) != 0)
            {
                throw new IOException($"Opened build output file identity could not be inspected: {sourcePath}. errno={Marshal.GetLastWin32Error()}");
            }

            return ReadOutputFileIdentity(buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static OutputFileIdentity ReadOutputFileIdentity (byte[] buffer)
    {
        if (OperatingSystem.IsLinux())
        {
            return new OutputFileIdentity(
                IsAvailable: true,
                Device: BitConverter.ToUInt64(buffer, LinuxDeviceOffset),
                Inode: BitConverter.ToUInt64(buffer, LinuxInodeOffset),
                Mode: BitConverter.ToInt32(buffer, GetLinuxFileModeOffset()));
        }

        if (OperatingSystem.IsMacOS())
        {
            return new OutputFileIdentity(
                IsAvailable: true,
                Device: BitConverter.ToUInt32(buffer, MacOsDeviceOffset),
                Inode: BitConverter.ToUInt64(buffer, MacOsInodeOffset),
                Mode: BitConverter.ToUInt16(buffer, MacOsFileModeOffset));
        }

        return OutputFileIdentity.Unavailable;
    }

    private static int GetLinuxFileModeOffset ()
    {
        return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? LinuxArm64FileModeOffset
            : LinuxFileModeOffset;
    }

    private static bool CanCapturePosixFileIdentity ()
    {
        return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    }

    private static async ValueTask<long> AddArtifactFileEntryAsync (
        string entryId,
        string entryRelativePath,
        string sourcePath,
        string artifactFilePath,
        List<BuildOutputManifestFileJsonContract> files,
        CancellationToken cancellationToken)
    {
        var sizeBytes = new FileInfo(artifactFilePath).Length;
        var sha256 = await ComputeFileSha256Async(
                artifactFilePath,
                sizeBytes,
                cancellationToken)
            .ConfigureAwait(false);
        var logicalPath = $"{entryId}/{entryRelativePath}";
        files.Add(new BuildOutputManifestFileJsonContract(
            entryId,
            logicalPath,
            sourcePath,
            $"{UcliStoragePathNames.BuildOutputDirectoryName}/{logicalPath}",
            sizeBytes,
            sha256));
        return sizeBytes;
    }

    private static string FormatOutputEntryId (int ordinal)
    {
        if (ordinal is < 1 or > 9999)
        {
            throw new InvalidOperationException($"Build output source entry ordinal is outside the manifest id range: {ordinal}");
        }

        return string.Create(
            11,
            ordinal,
            static (destination, value) =>
            {
                OutputEntryIdPrefix.AsSpan().CopyTo(destination);
                value.TryFormat(destination[7..], out _, "D4", null);
            });
    }

    private static string CombineSlashRelativePath (
        string root,
        string relativePath)
    {
        var path = root;
        var segments = relativePath.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            path = Path.Combine(path, segments[i]);
        }

        return path;
    }

    private static void EnsureUniqueLogicalPaths (IReadOnlyList<BuildOutputManifestFileJsonContract> files)
    {
        var logicalPaths = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < files.Count; i++)
        {
            if (!logicalPaths.Add(files[i].LogicalPath))
            {
                throw new InvalidOperationException($"Build output manifest logicalPath is duplicated: {files[i].LogicalPath}");
            }
        }
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
            throw new IOException(
                $"Build output file length changed while hashing: {filePath}.");
        }

        var finalLength = new FileInfo(filePath).Length;
        if (finalLength != expectedLength)
        {
            throw new IOException(
                $"Build output file length changed after hashing: {filePath}.");
        }

        return Sha256LowerHex.GetHashAndReset(hash);
    }

    private static async ValueTask<BuildArtifactAccountingResult> AccountExistingArtifactAsync (
        BuildArtifactKind kind,
        string artifactRoot,
        string path,
        string description,
        UcliCode missingCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var digest = await ComputeExistingArtifactSha256Async(path, cancellationToken).ConfigureAwait(false);
            return BuildArtifactAccountingResult.Success(CreateArtifactRef(kind, artifactRoot, path, digest));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildArtifactAccountingResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path is invalid. {exception.Message}"));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return BuildArtifactAccountingResult.Failure(ExecutionError.InternalError(
                $"{description} is missing: {path}. {exception.Message}",
                missingCode));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildArtifactAccountingResult.Failure(ExecutionError.InternalError(
                $"Failed to digest {description}: {path}. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }
    }

    private static async ValueTask<string> ComputeExistingArtifactSha256Async (
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureReadableArtifactFile(path);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(FileStreamBufferSize);
        try
        {
            using var stream = new FileStream(
                path,
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

                hash.AppendData(buffer, 0, bytesRead);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
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

    private static void EnsureReadableArtifactFile (string path)
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

        if (!FileSystemNodeClassifier.IsRegularFile(path, attributes))
        {
            throw new IOException($"Build artifact source must be a regular file: {path}");
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
        BuildArtifactKind kind,
        string artifactRoot,
        string path,
        string sha256)
    {
        return new BuildArtifactRef(
            kind,
            NormalizeArtifactRelativePath(artifactRoot, path),
            sha256);
    }

    private static string NormalizeArtifactRelativePath (
        string artifactRoot,
        string path)
    {
        var normalizedArtifactRoot = Path.GetFullPath(artifactRoot);
        var normalizedPath = Path.GetFullPath(path);
        if (!PathsAreSameOrNested(normalizedArtifactRoot, normalizedPath))
        {
            throw new InvalidOperationException(
                $"Build artifact path must resolve inside the artifact root. ArtifactRoot={normalizedArtifactRoot}, Path={normalizedPath}.");
        }

        var relativePath = Path.GetRelativePath(normalizedArtifactRoot, normalizedPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        if (string.IsNullOrWhiteSpace(relativePath)
            || relativePath.StartsWith("../", StringComparison.Ordinal)
            || string.Equals(relativePath, "..", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException(
                $"Build artifact path could not be normalized relative to the artifact root: {path}.");
        }

        return relativePath;
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

    private void VerifyManifestDigest (BuildOutputManifestJsonContract contract)
    {
        var calculatedDigest = outputManifestWriter.CalculateManifestDigest(contract.ToContent());
        if (!string.Equals(calculatedDigest, contract.ManifestDigest, StringComparison.Ordinal))
        {
            throw new OutputManifestDigestMismatchException(
                $"Expected={contract.ManifestDigest}, Actual={calculatedDigest}.");
        }
    }

    private readonly record struct OutputFileIdentity (
        bool IsAvailable,
        ulong Device,
        ulong Inode,
        int Mode)
    {
        public static OutputFileIdentity Unavailable => default;

        public bool IsRegularFile => (Mode & PosixFileTypeMask) == PosixRegularFileType;
    }

    private sealed record ResolvedOutputSourceEntry (
        string SourcePath,
        string Kind);

    private sealed record OutputSourceFileCandidate (
        string FullPath,
        string RelativePath);

    private sealed record OutputManifestArtifacts (
        BuildOutputManifestJsonContract Contract,
        BuildOutputManifestSummary Summary);

    private sealed record BuildArtifactAccountingResult (
        BuildArtifactRef? Artifact,
        ExecutionError? Error)
    {
        public bool IsSuccess => Artifact != null && Error == null;

        public static BuildArtifactAccountingResult Success (BuildArtifactRef artifact)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            return new BuildArtifactAccountingResult(artifact, null);
        }

        public static BuildArtifactAccountingResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new BuildArtifactAccountingResult(null, error);
        }
    }

    private sealed class OutputPathPolicyException : Exception
    {
        public OutputPathPolicyException (string message)
            : base(message)
        {
        }
    }

    private sealed class OutputManifestDigestMismatchException : Exception
    {
        public OutputManifestDigestMismatchException (string message)
            : base(message)
        {
        }
    }

    private sealed class OutputManifestArtifactDigestMismatchException : Exception
    {
        public OutputManifestArtifactDigestMismatchException (string message)
            : base(message)
        {
        }
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "lstat")]
    private static extern int LStat (
        string path,
        byte[] fileStatus);

    [DllImport("libc", SetLastError = true, EntryPoint = "fstat")]
    private static extern int FStat (
        IntPtr fileDescriptor,
        byte[] fileStatus);

}
