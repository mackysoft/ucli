using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MackySoft.FileSystem;
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
        Guid runId)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        if (runId == Guid.Empty)
        {
            return BuildRunArtifactPreparationResult.Failure(
                ExecutionError.InvalidArgument("Run id must not be empty."));
        }

        BuildRunArtifactPaths paths;
        var runDirectory = UcliStoragePathResolver.ResolveBuildRunDirectory(
            unityProject.RepositoryRoot,
            runId);
        paths = ResolvePaths(unityProject.RepositoryRoot, runId);

        try
        {
            if (!runDirectory.TryGetParent(out var buildRunsDirectory))
            {
                throw new InvalidOperationException(
                    $"Build-runs directory could not be resolved: {runDirectory.Value}");
            }

            var preparationLockPath = ContainedPath.Create(
                buildRunsDirectory,
                RootRelativePath.Parse(BuildRunPreparationLockFileName)).Target;
            using var preparationLock = FileExclusiveLock.Acquire(
                preparationLockPath,
                BuildRunPreparationLockTimeout,
                CancellationToken.None);

            if (File.Exists(runDirectory.Value) || Directory.Exists(runDirectory.Value))
            {
                return BuildRunArtifactPreparationResult.Failure(ExecutionError.InternalError(
                    $"Build run directory already exists: {runDirectory.Value}.",
                    BuildErrorCodes.BuildArtifactWriteFailed));
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(paths.ArtifactsDirectory);
            FileSystemAccessBoundary.EnsureSecureDirectory(paths.RunnerOutputDirectory);
            return BuildRunArtifactPreparationResult.Success(paths);
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
        BuildPipelineOutputLayout outputLayout)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(outputLayout);

        try
        {
            EnsureExpectedPathLayout(paths);
            EnsureContainedBuildPipelineOutputLayout(paths, outputLayout);
        }
        catch (InvalidOperationException exception)
        {
            return BuildRunArtifactPreparationResult.Failure(ExecutionError.InvalidArgument(
                $"BuildPipeline output layout is invalid. {exception.Message}",
                BuildErrorCodes.BuildInputsInvalid));
        }

        try
        {
            var locationPath = outputLayout.LocationPath;
            if (!locationPath.TryGetParent(out var parentDirectory))
            {
                throw new InvalidOperationException(
                    $"BuildPipeline output parent directory could not be resolved: {locationPath.Value}");
            }

            FileSystemAccessBoundary.EnsureSecureDirectory(parentDirectory);
            EnsureBuildPipelineOutputTargetDoesNotExist(locationPath);
            return BuildRunArtifactPreparationResult.Success(paths);
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
        catch (InvalidOperationException exception)
        {
            return BuildRunArtifactAccountingOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Build artifact path layout is invalid. {exception.Message}"));
        }

        try
        {
            if (!request.Paths.ArtifactsDirectory.TryGetParent(out var runDirectory))
            {
                throw new InvalidOperationException(
                    $"Build run directory could not be resolved: {request.Paths.ArtifactsDirectory.Value}");
            }

            var accountingLockPath = ContainedPath.Create(
                runDirectory,
                RootRelativePath.Parse(BuildRunAccountingLockFileName)).Target;
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
            sourcePath.Value,
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

    private static void EnsureReadableBuildReportSourceFile (AbsolutePath sourcePath)
    {
        if (!File.Exists(sourcePath.Value) && !Directory.Exists(sourcePath.Value))
        {
            throw new BuildReportSourceException($"BuildReport source file was not found: {sourcePath.Value}");
        }

        var attributes = File.GetAttributes(sourcePath.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new BuildReportSourceException($"BuildReport source file must not be a reparse point: {sourcePath.Value}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new BuildReportSourceException($"BuildReport source file must not be a directory: {sourcePath.Value}");
        }

        if (!FileSystemNodeClassifier.IsRegularFile(sourcePath, attributes))
        {
            throw new BuildReportSourceException($"BuildReport source file must be a regular file: {sourcePath.Value}");
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
        AbsolutePath repositoryRoot,
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
            ResolveArtifactPath(artifactsDirectory, UcliStoragePathNames.BuildMetadataFileName),
            ResolveArtifactPath(artifactsDirectory, UcliStoragePathNames.BuildReportFileName),
            ResolveArtifactPath(artifactsDirectory, UcliStoragePathNames.BuildLogFileName),
            ResolveArtifactPath(artifactsDirectory, UcliStoragePathNames.BuildOutputManifestFileName),
            runnerOutputDirectory,
            ResolveArtifactPath(artifactsDirectory, UcliStoragePathNames.BuildOutputDirectoryName));
    }

    private static AbsolutePath ResolveArtifactPath (
        AbsolutePath artifactsDirectory,
        string relativePath)
    {
        return ContainedPath.Create(
            artifactsDirectory,
            RootRelativePath.Parse(relativePath)).Target;
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

    private static void EnsureContainedBuildPipelineOutputLayout (
        BuildRunArtifactPaths paths,
        BuildPipelineOutputLayout outputLayout)
    {
        if (!ContainedPath.TryCreate(
            paths.RunnerOutputDirectory,
            outputLayout.LocationPath,
            out _,
            out _))
        {
            throw new InvalidOperationException(
                $"BuildPipeline output location must remain below the runner output directory: {outputLayout.LocationPath.Value}");
        }
    }

    private static void EnsureBuildPipelineOutputTargetDoesNotExist (AbsolutePath locationPath)
    {
        if (!File.Exists(locationPath.Value) && !Directory.Exists(locationPath.Value))
        {
            return;
        }

        var attributes = File.GetAttributes(locationPath.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"BuildPipeline output target must not be a reparse point: {locationPath.Value}");
        }

        throw new IOException($"BuildPipeline output target already exists: {locationPath.Value}");
    }

    private static void EnsureExpectedArtifactsDirectory (
        BuildRunArtifactPaths paths,
        AbsolutePath artifactsDirectory)
    {
        var expectedArtifactsDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
            paths.RepositoryRoot,
            paths.RunId);
        if (!artifactsDirectory.IsSameAs(expectedArtifactsDirectory))
        {
            throw new InvalidOperationException(
                $"Artifact directory must be {expectedArtifactsDirectory}: {paths.ArtifactsDirectory}");
        }
    }

    private static void EnsureExpectedPath (
        AbsolutePath artifactsDirectory,
        AbsolutePath actualPath,
        string expectedFileName)
    {
        var expectedPath = ResolveArtifactPath(artifactsDirectory, expectedFileName);
        if (!actualPath.IsSameAs(expectedPath))
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
        if (!paths.RunnerOutputDirectory.IsSameAs(expectedRunnerOutputDirectory))
        {
            throw new InvalidOperationException(
                $"Runner output directory must be {expectedRunnerOutputDirectory}: {paths.RunnerOutputDirectory}");
        }
    }

    private async ValueTask<OutputManifestArtifacts> CreateOutputManifestArtifactsAsync (
        BuildRunArtifactPaths paths,
        AbsolutePath artifactOutputDirectory,
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
                sourceEntry.SourcePath.Value));

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
        AbsolutePath? missingSourcePath = null;
        for (var i = 0; i < outputSources.Count; i++)
        {
            var outputSource = outputSources[i];
            var sourcePath = ResolveOutputSourcePath(paths, outputSource);
            EnsureOutputSourceOutsideArtifactRoot(paths, sourcePath);
            EnsureOutputSourceInsideRunnerOutputRoot(paths, sourcePath);
            if (!File.Exists(sourcePath.Value) && !Directory.Exists(sourcePath.Value))
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

        if (entries.Count == 0 && missingSourcePath is not null && allowEmptyOutputManifest)
        {
            return [];
        }

        if (missingSourcePath is not null)
        {
            throw new FileNotFoundException(
                $"Build output source entry was not found: {missingSourcePath.Value}",
                missingSourcePath.Value);
        }

        if (entries.Count == 0)
        {
            throw new InvalidOperationException("Successful build output accounting requires at least one existing output source entry.");
        }

        return entries;
    }

    private static AbsolutePath ResolveBuildReportSourcePath (
        BuildRunArtifactPaths paths,
        BuildReportSourceEntry source)
    {
        return ResolveRunnerOutputRelativeSourcePath(
            paths,
            source.RunnerOutputRelativePath!);
    }

    private static AbsolutePath ResolveOutputSourcePath (
        BuildRunArtifactPaths paths,
        BuildOutputSourceEntry outputSource)
    {
        return outputSource switch
        {
            BuildOutputSourceEntry.Absolute absolute => absolute.Path,
            BuildOutputSourceEntry.RunnerOutputRelative relative => ResolveRunnerOutputRelativeSourcePath(
                paths,
                relative.Path),
            _ => throw new ArgumentOutOfRangeException(nameof(outputSource), outputSource, "Unsupported build output source kind."),
        };
    }

    private static AbsolutePath ResolveRunnerOutputRelativeSourcePath (
        BuildRunArtifactPaths paths,
        RootRelativePath relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return ContainedPath.Create(paths.RunnerOutputDirectory, relativePath).Target;
    }

    private static void EnsureOutputSourceInsideRunnerOutputRoot (
        BuildRunArtifactPaths paths,
        AbsolutePath sourcePath)
    {
        if (!ContainedPath.TryCreate(
                paths.RunnerOutputDirectory,
                sourcePath,
                out _,
                out _))
        {
            throw new OutputPathPolicyException(
                $"Output source path must resolve inside the runner output root. Source={sourcePath.Value}, RunnerOutputRoot={paths.RunnerOutputDirectory.Value}.");
        }
    }

    private static void EnsureOutputSourceOutsideArtifactRoot (
        BuildRunArtifactPaths paths,
        AbsolutePath sourcePath)
    {
        if (paths.ArtifactsDirectory.IsSameOrAncestorOf(sourcePath))
        {
            throw new OutputPathPolicyException(
                $"Output source path must not resolve inside the artifact root. Source={sourcePath.Value}, ArtifactRoot={paths.ArtifactsDirectory.Value}.");
        }
    }

    private static void EnsureRunnerOutputSourcePathHasNoReparsePoint (
        AbsolutePath runnerOutputRoot,
        AbsolutePath sourcePath,
        string sourceKind)
    {
        if (!ContainedPath.TryCreate(
                runnerOutputRoot,
                sourcePath,
                out var containedSourcePath,
                out _))
        {
            throw new OutputPathPolicyException(
                $"Output source path must resolve inside the runner output root. Source={sourcePath.Value}, RunnerOutputRoot={runnerOutputRoot.Value}.");
        }

        EnsureRunnerOutputPathNodeIsNotReparsePoint(runnerOutputRoot, sourceKind);
        if (containedSourcePath!.RelativePath.IsRoot)
        {
            return;
        }

        var sourceAncestors = new Stack<AbsolutePath>();
        var currentPath = containedSourcePath.Target;
        while (!currentPath.IsSameAs(runnerOutputRoot))
        {
            sourceAncestors.Push(currentPath);
            if (!currentPath.TryGetParent(out currentPath))
            {
                throw new OutputPathPolicyException(
                    $"Output source path did not retain its proven runner output containment. Source={sourcePath.Value}, RunnerOutputRoot={runnerOutputRoot.Value}.");
            }
        }

        while (sourceAncestors.TryPop(out var sourceAncestor))
        {
            EnsureRunnerOutputPathNodeIsNotReparsePoint(sourceAncestor, sourceKind);
        }
    }

    private static void EnsureRunnerOutputPathNodeIsNotReparsePoint (
        AbsolutePath path,
        string sourceKind)
    {
        var attributes = File.GetAttributes(path.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"{sourceKind} path must not contain a reparse point: {path.Value}");
        }
    }

    private static string ResolveOutputSourceEntryKind (AbsolutePath sourcePath)
    {
        var attributes = File.GetAttributes(sourcePath.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build output source entry must not be a reparse point: {sourcePath.Value}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            return OutputEntryKindDirectory;
        }

        if (!FileSystemNodeClassifier.IsRegularFile(sourcePath, attributes))
        {
            throw new IOException($"Build output source entry must be a regular file or directory: {sourcePath.Value}");
        }

        return OutputEntryKindFile;
    }

    private async ValueTask<long> IngestOutputSourceEntryAsync (
        AbsolutePath artifactOutputDirectory,
        ResolvedOutputSourceEntry sourceEntry,
        string entryId,
        List<BuildOutputManifestFileJsonContract> files,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entryOutputDirectory = ContainedPath.Create(
            artifactOutputDirectory,
            RootRelativePath.Parse(entryId)).Target;
        FileSystemAccessBoundary.EnsureSecureDirectory(entryOutputDirectory);

        if (ContractLiteralCodec.Matches(sourceEntry.Kind, BuildOutputManifestEntryKind.File))
        {
            if (!sourceEntry.SourcePath.TryGetParent(out var sourceDirectory))
            {
                throw new IOException(
                    $"Build output file must have a lexical parent directory: {sourceEntry.SourcePath.Value}");
            }

            var fileName = ContainedPath.Create(
                sourceDirectory,
                sourceEntry.SourcePath).RelativePath;
            var portableFileName = GetPortableOutputRelativePath(
                fileName,
                sourceEntry.SourcePath);
            var artifactFilePath = ContainedPath.Create(entryOutputDirectory, fileName).Target;
            await CopyRegularFileAsync(sourceEntry.SourcePath, artifactFilePath, cancellationToken).ConfigureAwait(false);
            return await AddArtifactFileEntryAsync(
                    entryId,
                    portableFileName,
                    sourceEntry.SourcePath,
                    artifactFilePath,
                    files,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var candidates = EnumerateOutputSourceFileCandidates(sourceEntry.SourcePath);
        candidates.Sort(static (left, right) => string.CompareOrdinal(
            left.PortableRelativePath,
            right.PortableRelativePath));

        long totalBytes = 0;
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var artifactFilePath = ContainedPath.Create(
                entryOutputDirectory,
                candidate.RelativePath).Target;
            await CopyRegularFileAsync(candidate.FullPath, artifactFilePath, cancellationToken).ConfigureAwait(false);
            totalBytes += await AddArtifactFileEntryAsync(
                    entryId,
                    candidate.PortableRelativePath,
                    candidate.FullPath,
                    artifactFilePath,
                    files,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return totalBytes;
    }

    private static List<OutputSourceFileCandidate> EnumerateOutputSourceFileCandidates (AbsolutePath sourceDirectory)
    {
        EnsureOutputDirectoryNode(sourceDirectory);

        var files = new List<OutputSourceFileCandidate>();
        var pendingDirectories = new Stack<AbsolutePath>();
        pendingDirectories.Push(sourceDirectory);
        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            foreach (var entryPathText in Directory.EnumerateFileSystemEntries(currentDirectory.Value))
            {
                var entryPath = AbsolutePath.Parse(entryPathText);
                if (!ContainedPath.TryCreate(
                        sourceDirectory,
                        entryPath,
                        out var containedEntry,
                        out _))
                {
                    throw new IOException($"Build output entry escaped the output directory: {entryPath.Value}");
                }

                var attributes = File.GetAttributes(entryPath.Value);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"Build output entry must not be a reparse point: {entryPath.Value}");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pendingDirectories.Push(entryPath);
                    continue;
                }

                if (!FileSystemNodeClassifier.IsRegularFile(entryPath, attributes))
                {
                    throw new IOException($"Build output file must be a regular file: {entryPath.Value}");
                }

                var relativePath = containedEntry!.RelativePath;
                var portableRelativePath = GetPortableOutputRelativePath(relativePath, entryPath);
                files.Add(new OutputSourceFileCandidate(
                    entryPath,
                    relativePath,
                    portableRelativePath));
            }
        }

        return files;
    }

    private static async ValueTask CopyRegularFileAsync (
        AbsolutePath sourcePath,
        AbsolutePath destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureReadableOutputFile(sourcePath);
        var sourceIdentity = CaptureSourceFileIdentity(sourcePath, "Build output file");

        if (!destinationPath.TryGetParent(out var destinationDirectoryPath))
        {
            throw new InvalidOperationException(
                $"Artifact output directory path could not be resolved: {destinationPath.Value}");
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(destinationDirectoryPath);
        EnsureWritableArtifactPath(destinationPath);
        var buffer = ArrayPool<byte>.Shared.Rent(FileStreamBufferSize);
        try
        {
            using var sourceStream = new FileStream(
                sourcePath.Value,
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
                destinationPath.Value,
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
        AbsolutePath sourcePath,
        string sourceKind)
    {
        if (!CanCapturePosixFileIdentity())
        {
            return OutputFileIdentity.Unavailable;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(PosixFileStatusBufferSize);
        try
        {
            if (LStat(sourcePath.Value, buffer) != 0)
            {
                throw new IOException($"{sourceKind} identity could not be inspected: {sourcePath.Value}. errno={Marshal.GetLastWin32Error()}");
            }

            return ReadOutputFileIdentity(buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void EnsureOpenedSourceFileMatchesIdentity (
        AbsolutePath sourcePath,
        FileStream sourceStream,
        OutputFileIdentity expectedIdentity,
        string sourceKind,
        Action<AbsolutePath> validateWhenIdentityUnavailable)
    {
        if (!expectedIdentity.IsAvailable)
        {
            validateWhenIdentityUnavailable(sourcePath);
            return;
        }

        var actualIdentity = CaptureOpenedSourceFileIdentity(sourcePath, sourceStream, sourceKind);
        if (!actualIdentity.IsRegularFile)
        {
            throw new IOException($"{sourceKind} must be a regular file after opening: {sourcePath.Value}");
        }

        if (actualIdentity.Device != expectedIdentity.Device || actualIdentity.Inode != expectedIdentity.Inode)
        {
            throw new IOException($"{sourceKind} changed before it could be read: {sourcePath.Value}");
        }
    }

    private static OutputFileIdentity CaptureOpenedSourceFileIdentity (
        AbsolutePath sourcePath,
        FileStream sourceStream,
        string sourceKind)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(PosixFileStatusBufferSize);
        try
        {
            if (FStat(sourceStream.SafeFileHandle.DangerousGetHandle(), buffer) != 0)
            {
                throw new IOException($"Opened {sourceKind} identity could not be inspected: {sourcePath.Value}. errno={Marshal.GetLastWin32Error()}");
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
        string portableEntryRelativePath,
        AbsolutePath sourcePath,
        AbsolutePath artifactFilePath,
        List<BuildOutputManifestFileJsonContract> files,
        CancellationToken cancellationToken)
    {
        var sizeBytes = new FileInfo(artifactFilePath.Value).Length;
        var sha256 = await ComputeFileSha256Async(
                artifactFilePath,
                sizeBytes,
            cancellationToken)
            .ConfigureAwait(false);
        var logicalPath = $"{entryId}/{portableEntryRelativePath}";
        files.Add(new BuildOutputManifestFileJsonContract(
            entryId,
            logicalPath,
            sourcePath.Value,
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

    private static void EnsureOutputDirectoryNode (AbsolutePath outputDirectory)
    {
        var attributes = File.GetAttributes(outputDirectory.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build output directory must not be a reparse point: {outputDirectory.Value}");
        }

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException($"Build output path must be a directory: {outputDirectory.Value}");
        }
    }

    private static string GetPortableOutputRelativePath (
        RootRelativePath relativePath,
        AbsolutePath fullPath)
    {
        if (!UcliPortablePathAdapter.TryFormat(relativePath, out var portablePath))
        {
            throw new IOException(
                $"Build output file path cannot be represented by the portable artifact contract: {fullPath.Value}");
        }

        return portablePath;
    }

    private static async ValueTask<Sha256Digest> ComputeFileSha256Async (
        AbsolutePath filePath,
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
                filePath.Value,
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
                $"Build output file length changed while hashing: {filePath.Value}.");
        }

        var finalLength = new FileInfo(filePath.Value).Length;
        if (finalLength != expectedLength)
        {
            throw new IOException(
                $"Build output file length changed after hashing: {filePath.Value}.");
        }

        return Sha256LowerHex.GetHashAndReset(hash);
    }

    private static async ValueTask<BuildArtifactAccountingResult> AccountExistingArtifactAsync (
        BuildArtifactKind kind,
        AbsolutePath artifactRoot,
        AbsolutePath path,
        string description,
        UcliCode missingCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var digest = await ComputeExistingArtifactSha256Async(path, cancellationToken).ConfigureAwait(false);
            return BuildArtifactAccountingResult.Success(CreateArtifactRef(kind, artifactRoot, path, digest));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return BuildArtifactAccountingResult.Failure(ExecutionError.InternalError(
                $"{description} is missing: {path.Value}. {exception.Message}",
                missingCode));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BuildArtifactAccountingResult.Failure(ExecutionError.InternalError(
                $"Failed to digest {description}: {path.Value}. {exception.Message}",
                BuildErrorCodes.BuildArtifactWriteFailed));
        }
    }

    private static async ValueTask<Sha256Digest> ComputeExistingArtifactSha256Async (
        AbsolutePath path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureReadableArtifactFile(path);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(FileStreamBufferSize);
        try
        {
            using var stream = new FileStream(
                path.Value,
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

    private static void EnsureReadableOutputFile (AbsolutePath filePath)
    {
        if (!File.Exists(filePath.Value) && !Directory.Exists(filePath.Value))
        {
            throw new FileNotFoundException($"Build output file was not found: {filePath.Value}", filePath.Value);
        }

        var attributes = File.GetAttributes(filePath.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build output file must not be a reparse point: {filePath.Value}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"Build output file must not be a directory: {filePath.Value}");
        }

        if (!FileSystemNodeClassifier.IsRegularFile(filePath, attributes))
        {
            throw new IOException($"Build output file must be a regular file: {filePath.Value}");
        }
    }

    private static void EnsureReadableArtifactFile (AbsolutePath path)
    {
        if (!File.Exists(path.Value) && !Directory.Exists(path.Value))
        {
            throw new FileNotFoundException($"Build artifact was not found: {path.Value}", path.Value);
        }

        var attributes = File.GetAttributes(path.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build artifact source must not be a reparse point: {path.Value}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"Build artifact source must not be a directory: {path.Value}");
        }

        if (!FileSystemNodeClassifier.IsRegularFile(path, attributes))
        {
            throw new IOException($"Build artifact source must be a regular file: {path.Value}");
        }
    }

    private static async ValueTask<Sha256Digest> WriteTextAtomicallyAsync (
        AbsolutePath path,
        string text,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!path.TryGetParent(out var directoryPath))
        {
            throw new InvalidOperationException(
                $"Artifact directory path could not be resolved: {path.Value}");
        }

        var digest = ComputeUtf8Sha256(text);
        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
        var temporaryStream = FileUtilities.OpenAtomicWriteTemporaryFileInDirectory(
            directoryPath,
            out var tempPath);
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
        AbsolutePath artifactRoot,
        AbsolutePath path,
        Sha256Digest sha256)
    {
        var containedArtifact = ContainedPath.Create(artifactRoot, path);
        if (containedArtifact.RelativePath.IsRoot)
        {
            throw new InvalidOperationException(
                $"Build artifact path must be below the artifact root: {path.Value}.");
        }

        if (!UcliPortablePathAdapter.TryFormat(
                containedArtifact.RelativePath,
                out var portableArtifactPath))
        {
            throw new InvalidOperationException(
                $"Build artifact path cannot be represented by the portable artifact contract: {path.Value}.");
        }

        return new BuildArtifactRef(kind, portableArtifactPath, sha256);
    }

    private static void EnsureWritableArtifactPath (AbsolutePath path)
    {
        if (!File.Exists(path.Value) && !Directory.Exists(path.Value))
        {
            return;
        }

        var attributes = File.GetAttributes(path.Value);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Build artifact target must not be a reparse point: {path.Value}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"Build artifact target must not be a directory: {path.Value}");
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
        AbsolutePath SourcePath,
        string Kind);

    private sealed record OutputSourceFileCandidate (
        AbsolutePath FullPath,
        RootRelativePath RelativePath,
        string PortableRelativePath);

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
