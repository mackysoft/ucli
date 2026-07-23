using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private const string BuildRunAccountingLockFileName = ".account.lock";
    private const string BuildRunPreparationLockFileName = ".prepare.lock";

    private static readonly TimeSpan BuildRunAccountingLockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BuildRunPreparationLockTimeout = TimeSpan.FromSeconds(1);
    private static readonly string OutputEntryKindDirectory = TextVocabulary.GetText(BuildOutputManifestEntryKind.Directory);
    private static readonly string OutputEntryKindFile = TextVocabulary.GetText(BuildOutputManifestEntryKind.File);
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
        Guid runId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);

        BuildRunArtifactPaths paths;
        string runDirectory;
        try
        {
            runDirectory = UcliStoragePathResolver.ResolveBuildRunDirectory(
                unityProject.RepositoryRoot,
                runId);
            paths = ResolvePaths(unityProject.RepositoryRoot, runId);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildRunArtifactPreparationResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path is invalid. {exception.Message}"));
        }

        try
        {
            var buildRunsDirectory = Path.GetDirectoryName(runDirectory)
                ?? throw new InvalidOperationException(
                    $"Build-runs directory could not be resolved: {runDirectory}");
            var preparationLockPath = Path.Combine(
                buildRunsDirectory,
                BuildRunPreparationLockFileName);
            using var preparationLock = FileExclusiveLock.Acquire(
                preparationLockPath,
                BuildRunPreparationLockTimeout,
                CancellationToken.None);

            if (File.Exists(runDirectory) || Directory.Exists(runDirectory))
            {
                return BuildRunArtifactPreparationResult.Failure(ExecutionError.InternalError(
                    $"Build run directory already exists: {runDirectory}.",
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
        {
            return BuildRunArtifactPreparationResult.Failure(ExecutionError.InternalError(
                $"Failed to prepare build artifact directory. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }
    }

    /// <inheritdoc />
    public BuildRunArtifactPreparationResult PrepareBuildPipelineOutputLayout (
        BuildRunArtifactPaths paths,
        BuildTargetStableName buildTarget,
        IpcBuildOutputLayout outputLayout)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (!TextVocabulary.IsDefined(buildTarget))
        {
            throw new ArgumentOutOfRangeException(nameof(buildTarget), buildTarget, "Build target must be specified.");
        }
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

        try
        {
            var runDirectory = Path.GetDirectoryName(request.Paths.ArtifactsDirectory)
                ?? throw new InvalidOperationException(
                    $"Build run directory could not be resolved: {request.Paths.ArtifactsDirectory}");
            var accountingLockPath = Path.Combine(runDirectory, BuildRunAccountingLockFileName);
            using var accountingLock = await FileExclusiveLock
                .AcquireAsync(
                    accountingLockPath,
                    BuildRunAccountingLockTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return await AccountArtifactsWithExclusivePublicationAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InternalError(
                $"Build artifact accounting is already in progress. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestFailed));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to publish build output artifacts. {exception.Message}",
                BuildErrorCodes.BuildOutputManifestFailed));
        }
    }

    private async ValueTask<BuildRunArtifactAccountingOperationResult> AccountArtifactsWithExclusivePublicationAsync (
        BuildRunArtifactAccountingRequest request,
        CancellationToken cancellationToken)
    {
        using var outputPublication = BuildOutputPublication.Begin(request.Paths);

        OutputManifestArtifacts outputManifestArtifacts;
        try
        {
            outputManifestArtifacts = await CreateOutputManifestArtifactsAsync(
                    request.Paths,
                    outputPublication.StagingDirectory,
                    request.BuildTarget,
                    request.UnityBuildTarget,
                    request.OutputSources,
                    request.AllowEmptyOutputManifest,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RunnerOutputSourceMissingException exception)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InvalidArgument(
                exception.Message,
                BuildErrorCodes.BuildRunnerResultInvalid));
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

        BuildArtifactRef? buildReportRef = null;
        if (request.BuildReport != null)
        {
            var buildReportWriteResult = await AccountBuildReportAsync(
                    request.Paths,
                    request.BuildReport,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!buildReportWriteResult.IsSuccess)
            {
                return BuildRunArtifactAccountingOperationResult.Failure(buildReportWriteResult.Error!);
            }

            buildReportRef = buildReportWriteResult.Artifact!;
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

        var buildLogRef = buildLogAccountingResult.Artifact!;

        BuildRunArtifactAccountingResult accountingResult;
        try
        {
            VerifyManifestDigest(outputManifestArtifacts.Contract);
            var outputManifestJson = outputManifestWriter.Write(outputManifestArtifacts.Contract);
            var outputManifestDigest = ComputeUtf8Sha256(outputManifestJson);
            var commitMarkerTemporaryPath = await outputPublication.PrepareCommitMarkerAsync(
                    outputManifestJson,
                    cancellationToken)
                .ConfigureAwait(false);
            var preparedOutputManifestDigest = await ComputeExistingArtifactSha256Async(
                    commitMarkerTemporaryPath,
                    cancellationToken)
                .ConfigureAwait(false);
            if (outputManifestDigest != preparedOutputManifestDigest)
            {
                throw new OutputManifestArtifactDigestMismatchException(
                    $"Expected={outputManifestDigest}, Actual={preparedOutputManifestDigest}.");
            }

            var outputManifestRef = CreateArtifactRef(
                BuildArtifactKind.BuildOutputManifest,
                request.Paths.ArtifactsDirectory,
                request.Paths.OutputManifestJsonPath,
                preparedOutputManifestDigest);
            accountingResult = new BuildRunArtifactAccountingResult(
                buildReportRef,
                outputManifestRef,
                buildLogRef,
                outputManifestArtifacts.Summary);

            outputPublication.PublishOutput();
            await outputPublication.PublishCommitMarkerAsync(cancellationToken).ConfigureAwait(false);
            outputPublication.Complete();
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

        return BuildRunArtifactAccountingOperationResult.Success(accountingResult);
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

        if (request.Metadata.RunId != request.Paths.RunId)
        {
            return BuildArtifactRefWriteResult.Failure(ExecutionError.InvalidArgument(
                "Build metadata run identifier must match its artifact path run identifier."));
        }

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
            var artifacts = CreateMetadataArtifactRefs(request.Accounting);
            var buildJson = metadataWriter.Write(
                request.Metadata,
                artifacts);
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

    private async ValueTask<BuildArtifactAccountingResult> AccountBuildReportAsync (
        BuildRunArtifactPaths paths,
        BuildReportSourceEntry source,
        CancellationToken cancellationToken)
    {
        try
        {
            var buildReport = await ReadBuildReportArtifactAsync(paths, source, cancellationToken)
                .ConfigureAwait(false);
            var buildReportJson = JsonSerializer.Serialize(buildReport, IpcJsonSerializerOptions.Default);
            await WriteTextAtomicallyAsync(
                    paths.BuildReportJsonPath,
                    buildReportJson,
                    cancellationToken)
                .ConfigureAwait(false);

            var digest = await ComputeExistingArtifactSha256Async(
                    paths.BuildReportJsonPath,
                    cancellationToken)
                .ConfigureAwait(false);
            return BuildArtifactAccountingResult.Success(CreateArtifactRef(
                BuildArtifactKind.BuildReport,
                paths.ArtifactsDirectory,
                paths.BuildReportJsonPath,
                digest));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return BuildArtifactAccountingResult.Failure(ExecutionError.InvalidArgument(
                $"BuildReport source path is invalid. {exception.Message}",
                BuildErrorCodes.BuildRunnerResultInvalid));
        }
        catch (OutputPathPolicyException exception)
        {
            return BuildArtifactAccountingResult.Failure(ExecutionError.InvalidArgument(
                exception.Message,
                BuildErrorCodes.BuildRunnerResultInvalid));
        }
        catch (BuildReportSourceException exception)
        {
            return BuildArtifactAccountingResult.Failure(ExecutionError.InternalError(
                exception.Message,
                BuildErrorCodes.BuildReportMissing));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            return BuildArtifactAccountingResult.Failure(ExecutionError.InternalError(
                $"Failed to account BuildReport source. {exception.Message}",
                BuildErrorCodes.BuildReportMissing));
        }
    }

    private static async ValueTask<IpcBuildReportArtifact> ReadBuildReportArtifactAsync (
        BuildRunArtifactPaths paths,
        BuildReportSourceEntry source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Artifact != null)
        {
            return source.Artifact!;
        }

        var sourcePath = ResolveBuildReportSourcePath(paths, source);
        EnsureReadableBuildReportSourceFile(sourcePath);
        EnsureRunnerOutputSourcePathHasNoReparsePoint(
            paths.RunnerOutputDirectory,
            sourcePath,
            "BuildReport source");
        var sourceIdentity = CaptureSourceFileIdentity(sourcePath, "BuildReport source file");
        await using var stream = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        EnsureOpenedSourceFileMatchesIdentity(
            sourcePath,
            stream,
            sourceIdentity,
            "BuildReport source file",
            EnsureReadableBuildReportSourceFile);
        IpcBuildReportArtifact? buildReport;
        try
        {
            buildReport = await JsonSerializer.DeserializeAsync<IpcBuildReportArtifact>(
                    stream,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            throw new BuildReportSourceException(
                $"BuildReport source violates the normalized contract. {exception.Message}");
        }

        if (buildReport == null)
        {
            throw new BuildReportSourceException("BuildReport source is not a valid uCLI BuildReport JSON artifact.");
        }

        return buildReport;
    }

    private static void EnsureReadableBuildReportSourceFile (string sourcePath)
    {
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            throw new BuildReportSourceException($"BuildReport source file was not found: {sourcePath}");
        }

        var attributes = File.GetAttributes(sourcePath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new BuildReportSourceException($"BuildReport source file must not be a reparse point: {sourcePath}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new BuildReportSourceException($"BuildReport source file must not be a directory: {sourcePath}");
        }

        if (!FileSystemNodeClassifier.IsRegularFile(sourcePath, attributes))
        {
            throw new BuildReportSourceException($"BuildReport source file must be a regular file: {sourcePath}");
        }
    }

    private static IReadOnlyList<BuildArtifactRef> CreateMetadataArtifactRefs (BuildRunArtifactAccountingResult accounting)
    {
        if (accounting.BuildReport == null)
        {
            return
            [
                accounting.BuildOutputManifest,
                accounting.BuildLog,
            ];
        }

        return
        [
            accounting.BuildReport,
            accounting.BuildOutputManifest,
            accounting.BuildLog,
        ];
    }

    private static BuildRunArtifactPaths ResolvePaths (
        string repositoryRoot,
        Guid runId)
    {
        var artifactsDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
            repositoryRoot,
            runId);
        var runnerOutputDirectory = UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
            repositoryRoot,
            runId);

        return new BuildRunArtifactPaths(
            repositoryRoot,
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
        var artifactsDirectory = paths.ArtifactsDirectory;
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
    }

    private static void EnsureExpectedBuildPipelineOutputLayout (
        BuildRunArtifactPaths paths,
        BuildTargetStableName buildTarget,
        IpcBuildOutputLayout outputLayout)
    {
        if (!IpcBuildOutputLayoutResolver.TryResolve(
            paths.RunnerOutputDirectory,
            buildTarget,
            androidAppBundle: false,
            out var expectedLayout))
        {
            throw new InvalidOperationException($"Build target does not have a deterministic BuildPipeline output layout: {buildTarget}");
        }

        if (outputLayout.Shape != expectedLayout!.Shape)
        {
            throw new InvalidOperationException(
                $"BuildPipeline output layout shape must be {expectedLayout.Shape}: {outputLayout.Shape}");
        }

        if (!PathIdentity.IsSamePath(outputLayout.LocationPathName, expectedLayout.LocationPathName))
        {
            throw new InvalidOperationException(
                $"BuildPipeline output locationPathName must be {expectedLayout.LocationPathName}: {outputLayout.LocationPathName}");
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
        var expectedArtifactsDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
            paths.RepositoryRoot,
            paths.RunId);
        if (!PathIdentity.IsSamePath(artifactsDirectory, expectedArtifactsDirectory))
        {
            throw new InvalidOperationException(
                $"Artifact directory must be {expectedArtifactsDirectory}: {paths.ArtifactsDirectory}");
        }
    }

    private static void EnsureExpectedPath (
        string artifactsDirectory,
        string actualPath,
        string expectedFileName)
    {
        var expectedPath = Path.Combine(artifactsDirectory, expectedFileName);
        if (!PathIdentity.IsSamePath(actualPath, expectedPath))
        {
            throw new InvalidOperationException(
                $"Artifact path must be {expectedPath}: {actualPath}");
        }
    }

    private static void EnsureExpectedRunnerOutputDirectory (BuildRunArtifactPaths paths)
    {
        var expectedRunnerOutputDirectory = UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
            paths.RepositoryRoot,
            paths.RunId);
        if (!PathIdentity.IsSamePath(paths.RunnerOutputDirectory, expectedRunnerOutputDirectory))
        {
            throw new InvalidOperationException(
                $"Runner output directory must be {expectedRunnerOutputDirectory}: {paths.RunnerOutputDirectory}");
        }
    }

    private async ValueTask<OutputManifestArtifacts> CreateOutputManifestArtifactsAsync (
        BuildRunArtifactPaths paths,
        string artifactOutputDirectory,
        BuildTargetStableName buildTarget,
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

        FileSystemAccessBoundary.EnsureSecureDirectory(artifactOutputDirectory);
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
                    artifactOutputDirectory,
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
            var sourcePath = ResolveOutputSourcePath(paths, outputSource);
            EnsureOutputSourceOutsideArtifactRoot(paths, sourcePath);
            EnsureOutputSourceInsideRunnerOutputRoot(paths, sourcePath);
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            {
                if (outputSource is BuildOutputSourceEntry.RunnerOutputRelative)
                {
                    throw new RunnerOutputSourceMissingException(
                        $"Build runner result declared an output source that does not exist: {sourcePath}");
                }

                missingSourcePath = sourcePath;
                continue;
            }

            EnsureRunnerOutputSourcePathHasNoReparsePoint(
                paths.RunnerOutputDirectory,
                sourcePath,
                "Build output source");
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

    private static string ResolveBuildReportSourcePath (
        BuildRunArtifactPaths paths,
        BuildReportSourceEntry source)
    {
        return ResolveRunnerOutputRelativeSourcePath(
            paths,
            source.RunnerOutputRelativePath!,
            "BuildReport source");
    }

    private static string ResolveOutputSourcePath (
        BuildRunArtifactPaths paths,
        BuildOutputSourceEntry outputSource)
    {
        return outputSource switch
        {
            BuildOutputSourceEntry.Absolute absolute => absolute.Path,
            BuildOutputSourceEntry.RunnerOutputRelative relative => ResolveRunnerOutputRelativeSourcePath(
                paths,
                relative.Path,
                "Output source"),
            _ => throw new ArgumentOutOfRangeException(nameof(outputSource), outputSource, "Unsupported build output source kind."),
        };
    }

    private static string ResolveRunnerOutputRelativeSourcePath (
        BuildRunArtifactPaths paths,
        BuildRunnerOutputPath relativePath,
        string sourceKind)
    {
        var result = RepositoryPathNormalizer.TryNormalize(paths.RunnerOutputDirectory, relativePath.Value);
        if (!result.IsSuccess)
        {
            throw new OutputPathPolicyException($"{sourceKind} path must resolve inside the runner output root: {relativePath}. {result.DiagnosticMessage}");
        }

        return result.FullPath!;
    }

    private static void EnsureOutputSourceInsideRunnerOutputRoot (
        BuildRunArtifactPaths paths,
        string sourcePath)
    {
        var runnerOutputRoot = Path.GetFullPath(paths.RunnerOutputDirectory);
        if (!RepositoryPathNormalizer.TryNormalize(runnerOutputRoot, sourcePath).IsSuccess)
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
        if (RepositoryPathNormalizer.TryNormalize(artifactRoot, sourcePath).IsSuccess)
        {
            throw new OutputPathPolicyException(
                $"Output source path must not resolve inside the artifact root. Source={sourcePath}, ArtifactRoot={artifactRoot}.");
        }
    }

    private static void EnsureRunnerOutputSourcePathHasNoReparsePoint (
        string runnerOutputRoot,
        string sourcePath,
        string sourceKind)
    {
        var rootPath = Path.GetFullPath(runnerOutputRoot);
        var relativePathResult = RepositoryPathNormalizer.TryNormalize(rootPath, sourcePath);
        if (!relativePathResult.IsSuccess)
        {
            throw new OutputPathPolicyException(
                $"Output source path must resolve inside the runner output root. Source={sourcePath}, RunnerOutputRoot={rootPath}.");
        }

        var relativePath = relativePathResult.RepositoryRelativeSlashPath!;
        EnsureRunnerOutputPathNodeIsNotReparsePoint(rootPath, sourceKind);
        if (string.Equals(relativePath, ".", StringComparison.Ordinal))
        {
            return;
        }

        var currentPath = rootPath;
        var segments = relativePath.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            currentPath = Path.Combine(currentPath, segments[i]);
            EnsureRunnerOutputPathNodeIsNotReparsePoint(currentPath, sourceKind);
        }
    }

    private static void EnsureRunnerOutputPathNodeIsNotReparsePoint (
        string path,
        string sourceKind)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"{sourceKind} path must not contain a reparse point: {path}");
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
        string artifactOutputDirectory,
        ResolvedOutputSourceEntry sourceEntry,
        string entryId,
        List<BuildOutputManifestFileJsonContract> files,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entryOutputDirectory = Path.Combine(artifactOutputDirectory, entryId);
        FileSystemAccessBoundary.EnsureSecureDirectory(entryOutputDirectory);

        if (TextVocabulary.Matches(sourceEntry.Kind, BuildOutputManifestEntryKind.File))
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

                var relativePathResult = RepositoryPathNormalizer.TryNormalize(sourceRootFullPath, fullPath);
                if (!relativePathResult.IsSuccess)
                {
                    throw new IOException($"Build output file path escaped the output directory: {fullPath}");
                }

                var relativePath = relativePathResult.RepositoryRelativeSlashPath!;
                EnsureSafeOutputRelativePath(relativePath, fullPath);
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
        var sourceIdentity = CaptureSourceFileIdentity(sourcePath, "Build output file");

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
            EnsureOpenedSourceFileMatchesIdentity(
                sourcePath,
                sourceStream,
                sourceIdentity,
                "Build output file",
                EnsureReadableOutputFile);

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

    private static OutputFileIdentity CaptureSourceFileIdentity (
        string sourcePath,
        string sourceKind)
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
                throw new IOException($"{sourceKind} identity could not be inspected: {sourcePath}. errno={Marshal.GetLastWin32Error()}");
            }

            return ReadOutputFileIdentity(buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void EnsureOpenedSourceFileMatchesIdentity (
        string sourcePath,
        FileStream sourceStream,
        OutputFileIdentity expectedIdentity,
        string sourceKind,
        Action<string> validateWhenIdentityUnavailable)
    {
        if (!expectedIdentity.IsAvailable)
        {
            validateWhenIdentityUnavailable(sourcePath);
            return;
        }

        var actualIdentity = CaptureOpenedSourceFileIdentity(sourcePath, sourceStream, sourceKind);
        if (!actualIdentity.IsRegularFile)
        {
            throw new IOException($"{sourceKind} must be a regular file after opening: {sourcePath}");
        }

        if (actualIdentity.Device != expectedIdentity.Device || actualIdentity.Inode != expectedIdentity.Inode)
        {
            throw new IOException($"{sourceKind} changed before it could be read: {sourcePath}");
        }
    }

    private static OutputFileIdentity CaptureOpenedSourceFileIdentity (
        string sourcePath,
        FileStream sourceStream,
        string sourceKind)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(PosixFileStatusBufferSize);
        try
        {
            if (FStat(sourceStream.SafeFileHandle.DangerousGetHandle(), buffer) != 0)
            {
                throw new IOException($"Opened {sourceKind} identity could not be inspected: {sourcePath}. errno={Marshal.GetLastWin32Error()}");
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

    private static async ValueTask<Sha256Digest> ComputeFileSha256Async (
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

    private static async ValueTask<Sha256Digest> ComputeExistingArtifactSha256Async (
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

    private static async ValueTask<Sha256Digest> WriteTextAtomicallyAsync (
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
        var temporaryStream = FileUtilities.OpenAtomicWriteTemporaryFileInDirectory(directoryPath, out var tempPath);
        var temporaryFileOwned = true;

        try
        {
            using (temporaryStream)
            using (var writer = new StreamWriter(temporaryStream, Utf8NoBom))
            {
                await writer
                    .WriteAsync(text.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
            }

            FileSystemAccessBoundary.EnsureSecureFile(tempPath);
            await FileUtilities.PublishAtomicWriteTemporaryFileAsync(
                    tempPath,
                    path,
                    cancellationToken)
                .ConfigureAwait(false);
            temporaryFileOwned = false;
            FileSystemAccessBoundary.EnsureSecureFile(path);
            return digest;
        }
        finally
        {
            if (temporaryFileOwned)
            {
                FileUtilities.DeleteIfExists(tempPath);
            }
        }
    }

    private static Sha256Digest ComputeUtf8Sha256 (string text)
    {
        using var hashWriter = new Utf8Sha256HashWriter();
        hashWriter.Append(text);
        return hashWriter.GetHashAndReset();
    }

    private static BuildArtifactRef CreateArtifactRef (
        BuildArtifactKind kind,
        string artifactRoot,
        string path,
        Sha256Digest sha256)
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
        var result = RepositoryPathNormalizer.TryNormalize(normalizedArtifactRoot, normalizedPath);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Build artifact path must resolve inside the artifact root. ArtifactRoot={normalizedArtifactRoot}, Path={normalizedPath}.");
        }

        var relativePath = result.RepositoryRelativeSlashPath!;
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

    private void VerifyManifestDigest (BuildOutputManifestJsonContract contract)
    {
        var calculatedDigest = outputManifestWriter.CalculateManifestDigest(contract.ToContent());
        if (calculatedDigest != contract.ManifestDigest)
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

    private sealed class RunnerOutputSourceMissingException : Exception
    {
        public RunnerOutputSourceMissingException (string message)
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

    private sealed class BuildReportSourceException : Exception
    {
        public BuildReportSourceException (string message)
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
