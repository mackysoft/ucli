using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Assurance.Build;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class FileBuildRunArtifactStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Prepare_CreatesBuildRunArtifactLayout ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-layout");
        var store = CreateStore();
        var project = CreateProject(scope);

        var result = store.Prepare(project, "run-1");

        Assert.True(result.IsSuccess);
        var paths = Assert.IsType<BuildRunArtifactPaths>(result.Paths);
        Assert.True(Directory.Exists(paths.ArtifactsDirectory));
        Assert.True(Directory.Exists(paths.RunnerOutputDirectory));
        Assert.False(Directory.Exists(paths.ArtifactOutputDirectory));
        Assert.Equal(
            Path.Combine(
                scope.FullPath,
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "fingerprint",
                UcliStoragePathNames.ArtifactsDirectoryName,
                UcliStoragePathNames.BuildArtifactsDirectoryName,
                "run-1"),
            paths.ArtifactsDirectory);
        Assert.Equal(
            Path.Combine(
                scope.FullPath,
                UcliStoragePathNames.UcliDirectoryName,
                UcliStoragePathNames.LocalDirectoryName,
                UcliStoragePathNames.FingerprintsDirectoryName,
                "fingerprint",
                UcliStoragePathNames.WorkDirectoryName,
                UcliStoragePathNames.BuildWorkDirectoryName,
                "run-1",
                UcliStoragePathNames.BuildOutputDirectoryName),
            paths.RunnerOutputDirectory);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Prepare_WhenBuildRunArtifactDirectoryAlreadyExists_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-existing-output");
        var store = CreateStore();
        var project = CreateProject(scope);

        var firstResult = store.Prepare(project, "run-1");
        var secondResult = store.Prepare(project, "run-1");

        Assert.True(firstResult.IsSuccess);
        Assert.False(secondResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(secondResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Prepare_WhenBuildRunArtifactDirectoryContainsLegacyArtifact_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-existing-legacy");
        var store = CreateStore();
        var project = CreateProject(scope);
        var artifactsDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
            project.RepositoryRoot,
            project.ProjectFingerprint,
            "run-1");
        Directory.CreateDirectory(artifactsDirectory);
        File.WriteAllText(Path.Combine(artifactsDirectory, "build-summary.json"), "{}");

        var result = store.Prepare(project, "run-1");

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PrepareBuildPipelineOutputLayout_WithResolvedLayout_CreatesPlayerParentDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-player-layout");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        Assert.True(IpcBuildOutputLayoutResolver.TryResolve(paths.RunnerOutputDirectory, "standaloneLinux64", out var layout));

        var result = store.PrepareBuildPipelineOutputLayout(paths, "standaloneLinux64", layout!);

        Assert.True(result.IsSuccess);
        Assert.True(Directory.Exists(Path.GetDirectoryName(layout!.LocationPathName)));
        Assert.False(File.Exists(layout.LocationPathName));
        Assert.False(Directory.Exists(layout.LocationPathName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PrepareBuildPipelineOutputLayout_WhenLocationPathNameAlreadyExists_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-player-collision");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        Assert.True(IpcBuildOutputLayoutResolver.TryResolve(paths.RunnerOutputDirectory, "standaloneLinux64", out var layout));
        WriteUtf8(layout!.LocationPathName, "existing player");

        var result = store.PrepareBuildPipelineOutputLayout(paths, "standaloneLinux64", layout);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PrepareBuildPipelineOutputLayout_WhenPlayerParentCannotBeCreated_ReturnsBuildArtifactWriteFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-player-parent-blocked");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        Assert.True(IpcBuildOutputLayoutResolver.TryResolve(paths.RunnerOutputDirectory, "standaloneLinux64", out var layout));
        WriteUtf8(Path.Combine(paths.RunnerOutputDirectory, "player"), "blocking file");

        var result = store.PrepareBuildPipelineOutputLayout(paths, "standaloneLinux64", layout!);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PrepareBuildPipelineOutputLayout_WhenTargetLayoutIsUnsupported_ReturnsBuildInputsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-player-unsupported");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var layout = new IpcBuildOutputLayout(
            Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
            LocationPathName: Path.Combine(paths.RunnerOutputDirectory, "player", "Player"));

        var result = store.PrepareBuildPipelineOutputLayout(paths, "switch", layout);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildInputsInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_ThenWriteMetadataAsync_WritesRequiredArtifactsAndManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "write-artifacts");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var directorySourcePath = Path.Combine(paths.RunnerOutputDirectory, "player");
        var fileSourcePath = Path.Combine(paths.RunnerOutputDirectory, "Game.x86_64");
        var zConfigSourcePath = Path.Combine(directorySourcePath, "Data", "z-config.json");
        var aConfigSourcePath = Path.Combine(directorySourcePath, "Data", "a-config.json");
        var zConfigBytes = WriteUtf8(zConfigSourcePath, "{\"quality\":\"high\"}\n");
        var aConfigBytes = WriteUtf8(aConfigSourcePath, "{\"quality\":\"low\"}\n");
        var playerBytes = WriteUtf8(fileSourcePath, "player binary\n");
        var buildLogBytes = WriteUtf8(paths.BuildLogPath, "build log\n");

        var accountingOperation = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths, directorySourcePath, fileSourcePath),
            CancellationToken.None);

        Assert.True(accountingOperation.IsSuccess);
        var result = Assert.IsType<BuildRunArtifactAccountingResult>(accountingOperation.Result);
        Assert.NotNull(result.BuildReport);
        Assert.Equal(BuildArtifactKind.BuildReport, result.BuildReport!.Kind);
        Assert.Equal(BuildArtifactKind.BuildOutputManifest, result.BuildOutputManifest.Kind);
        Assert.Equal(BuildArtifactKind.BuildLog, result.BuildLog.Kind);
        Assert.Equal(Sha256LowerHex.Compute(buildLogBytes), result.BuildLog.Digest);
        var buildReportBytes = await File.ReadAllBytesAsync(paths.BuildReportJsonPath, CancellationToken.None);
        Assert.Equal(Sha256LowerHex.Compute(buildReportBytes), result.BuildReport.Digest);

        var metadataWriteResult = await store.WriteMetadataAsync(
            new BuildRunMetadataWriteRequest(
                paths,
                CreateMetadata(paths.RunId),
                result),
            CancellationToken.None);

        Assert.True(metadataWriteResult.IsSuccess);
        var buildRef = Assert.IsType<BuildArtifactRef>(metadataWriteResult.Artifact);
        Assert.Equal(BuildArtifactKind.Build, buildRef.Kind);
        AssertLowerSha256(buildRef.Digest);

        var topLevelArtifactNames = Directory
            .EnumerateFileSystemEntries(paths.ArtifactsDirectory)
            .Select(static path => Path.GetFileName(path) ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                UcliStoragePathNames.BuildReportFileName,
                UcliStoragePathNames.BuildMetadataFileName,
                UcliStoragePathNames.BuildLogFileName,
                UcliStoragePathNames.BuildOutputDirectoryName,
                UcliStoragePathNames.BuildOutputManifestFileName,
            ],
            topLevelArtifactNames);
        Assert.DoesNotContain("build-summary.json", topLevelArtifactNames);
        Assert.DoesNotContain("profile-snapshot.json", topLevelArtifactNames);
        Assert.DoesNotContain("lifecycle.json", topLevelArtifactNames);
        Assert.DoesNotContain("manifest.json", topLevelArtifactNames);

        using var outputManifest = JsonDocument.Parse(await File.ReadAllTextAsync(paths.OutputManifestJsonPath, CancellationToken.None));
        var outputRoot = outputManifest.RootElement;
        var target = outputRoot.GetProperty("target");
        Assert.Equal("standaloneLinux64", target.GetProperty("stableName").GetString());
        Assert.Equal("StandaloneLinux64", target.GetProperty("unityBuildTarget").GetString());
        var entries = outputRoot.GetProperty("entries");
        Assert.Equal(2, entries.GetArrayLength());
        Assert.Equal("output-0001", entries[0].GetProperty("id").GetString());
        Assert.Equal("directory", entries[0].GetProperty("kind").GetString());
        Assert.Equal(Path.GetFullPath(directorySourcePath), entries[0].GetProperty("sourcePath").GetString());
        Assert.Equal("output-0002", entries[1].GetProperty("id").GetString());
        Assert.Equal("file", entries[1].GetProperty("kind").GetString());
        Assert.Equal(Path.GetFullPath(fileSourcePath), entries[1].GetProperty("sourcePath").GetString());
        Assert.Equal(2, outputRoot.GetProperty("entryCount").GetInt32());
        Assert.Equal(3, outputRoot.GetProperty("fileCount").GetInt32());
        Assert.Equal(aConfigBytes.Length + zConfigBytes.Length + playerBytes.Length, outputRoot.GetProperty("totalBytes").GetInt64());
        Assert.Equal(result.OutputManifest.ManifestDigest, outputRoot.GetProperty("manifestDigest").GetString());
        Assert.Equal(2, result.OutputManifest.EntryCount);
        Assert.Equal(3, result.OutputManifest.FileCount);
        Assert.Equal(aConfigBytes.Length + zConfigBytes.Length + playerBytes.Length, result.OutputManifest.TotalBytes);
        AssertLowerSha256(result.OutputManifest.ManifestDigest);

        var files = outputRoot.GetProperty("files");
        Assert.Equal("output-0001", files[0].GetProperty("entryId").GetString());
        Assert.Equal("output-0001/Data/a-config.json", files[0].GetProperty("logicalPath").GetString());
        Assert.Equal(Path.GetFullPath(aConfigSourcePath), files[0].GetProperty("sourcePath").GetString());
        Assert.Equal("output/output-0001/Data/a-config.json", files[0].GetProperty("artifactPath").GetString());
        Assert.Equal(aConfigBytes.Length, files[0].GetProperty("sizeBytes").GetInt64());
        Assert.Equal(Sha256LowerHex.Compute(aConfigBytes), files[0].GetProperty("sha256").GetString());
        Assert.Equal("output-0001", files[1].GetProperty("entryId").GetString());
        Assert.Equal("output-0001/Data/z-config.json", files[1].GetProperty("logicalPath").GetString());
        Assert.Equal(Path.GetFullPath(zConfigSourcePath), files[1].GetProperty("sourcePath").GetString());
        Assert.Equal("output/output-0001/Data/z-config.json", files[1].GetProperty("artifactPath").GetString());
        Assert.Equal(zConfigBytes.Length, files[1].GetProperty("sizeBytes").GetInt64());
        Assert.Equal(Sha256LowerHex.Compute(zConfigBytes), files[1].GetProperty("sha256").GetString());
        Assert.Equal("output-0002", files[2].GetProperty("entryId").GetString());
        Assert.Equal("output-0002/Game.x86_64", files[2].GetProperty("logicalPath").GetString());
        Assert.Equal(Path.GetFullPath(fileSourcePath), files[2].GetProperty("sourcePath").GetString());
        Assert.Equal("output/output-0002/Game.x86_64", files[2].GetProperty("artifactPath").GetString());
        Assert.Equal(playerBytes.Length, files[2].GetProperty("sizeBytes").GetInt64());
        Assert.Equal(Sha256LowerHex.Compute(playerBytes), files[2].GetProperty("sha256").GetString());
        await AssertFileSha256Async(
            Path.Combine(paths.ArtifactOutputDirectory, "output-0001", "Data", "a-config.json"),
            files[0].GetProperty("sha256").GetString()!);
        await AssertFileSha256Async(
            Path.Combine(paths.ArtifactOutputDirectory, "output-0001", "Data", "z-config.json"),
            files[1].GetProperty("sha256").GetString()!);
        await AssertFileSha256Async(
            Path.Combine(paths.ArtifactOutputDirectory, "output-0002", "Game.x86_64"),
            files[2].GetProperty("sha256").GetString()!);
        var recalculatedManifestDigest = new BuildOutputManifestJsonContractWriter().CalculateManifestDigest(
            ReadOutputManifestContent(outputRoot));
        Assert.Equal(recalculatedManifestDigest, result.OutputManifest.ManifestDigest);
        Assert.NotEqual(recalculatedManifestDigest, result.BuildOutputManifest.Digest);
        await AssertFileSha256Async(paths.OutputManifestJsonPath, result.BuildOutputManifest.Digest);

        using var buildMetadata = JsonDocument.Parse(await File.ReadAllTextAsync(paths.BuildJsonPath, CancellationToken.None));
        var buildRoot = buildMetadata.RootElement;
        Assert.Equal(1, buildRoot.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("run-1", buildRoot.GetProperty("runId").GetString());
        Assert.False(buildRoot.TryGetProperty("project", out _));
        Assert.False(buildRoot.TryGetProperty("input", out _));
        Assert.False(buildRoot.TryGetProperty("output", out _));
        Assert.False(buildRoot.TryGetProperty("dirtyState", out _));
        Assert.False(buildRoot.GetProperty("profile").TryGetProperty("output", out _));
        Assert.Equal("buildPipeline", buildRoot.GetProperty("runner").GetProperty("kind").GetString());
        Assert.Equal("file", buildRoot.GetProperty("runner").GetProperty("outputLayout").GetProperty("shape").GetString());
        Assert.Equal(
            "/repo/.ucli/local/fingerprints/fingerprint/work/build/run-1/output/player/Player",
            buildRoot.GetProperty("runner").GetProperty("outputLayout").GetProperty("locationPathName").GetString());
        var inputs = buildRoot.GetProperty("inputs");
        Assert.Equal("explicit", inputs.GetProperty("inputKind").GetString());
        Assert.Equal("standaloneLinux64", inputs.GetProperty("target").GetProperty("stableName").GetString());
        Assert.Equal("StandaloneLinux64", inputs.GetProperty("target").GetProperty("unityBuildTarget").GetString());

        var artifacts = buildRoot.GetProperty("artifacts");
        Assert.Equal(
            [
                GetArtifactKey(BuildArtifactKind.BuildReport),
                GetArtifactKey(BuildArtifactKind.BuildOutputManifest),
                GetArtifactKey(BuildArtifactKind.BuildLog),
            ],
            artifacts.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.False(artifacts.TryGetProperty(GetArtifactKey(BuildArtifactKind.Build), out _));
        AssertArtifactRef(
            artifacts.GetProperty(GetArtifactKey(BuildArtifactKind.BuildReport)),
            UcliStoragePathNames.BuildReportFileName,
            result.BuildReport.Digest);
        AssertArtifactRef(
            artifacts.GetProperty(GetArtifactKey(BuildArtifactKind.BuildOutputManifest)),
            UcliStoragePathNames.BuildOutputManifestFileName,
            result.BuildOutputManifest.Digest);
        AssertArtifactRef(
            artifacts.GetProperty(GetArtifactKey(BuildArtifactKind.BuildLog)),
            UcliStoragePathNames.BuildLogFileName,
            result.BuildLog.Digest);
        await AssertFileSha256Async(paths.BuildJsonPath, buildRef.Digest);
        await AssertFileSha256Async(paths.BuildReportJsonPath, result.BuildReport.Digest);
        await AssertFileSha256Async(paths.BuildLogPath, result.BuildLog.Digest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenOutputContainsSymbolicLink_ReturnsOutputManifestFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-symlink");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var targetPath = scope.WriteFile("target.txt", "linked output");
        var linkPath = Path.Combine(paths.RunnerOutputDirectory, "build");
        if (!TryCreateFileSymbolicLink(linkPath, targetPath))
        {
            return;
        }

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        Assert.Contains("reparse point", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenOutputSourceContainsSymbolicLinkAncestor_ReturnsOutputManifestFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-symlink-ancestor");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var outputSourceDirectory = Path.Combine(paths.RunnerOutputDirectory, "build");
        Directory.CreateDirectory(outputSourceDirectory);
        var targetDirectory = scope.CreateDirectory("outside-output");
        var targetFilePath = Path.Combine(targetDirectory, "payload.txt");
        WriteUtf8(targetFilePath, "external output");
        var linkPath = Path.Combine(outputSourceDirectory, "linked");
        if (!TryCreateDirectorySymbolicLink(linkPath, targetDirectory))
        {
            return;
        }

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths, Path.Combine(linkPath, "payload.txt")),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        Assert.Contains("reparse point", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenBuildReportSourceContainsSymbolicLinkAncestor_ReturnsBuildReportMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "build-report-symlink-ancestor");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var outputSourcePath = Path.Combine(paths.RunnerOutputDirectory, "build");
        WriteUtf8(outputSourcePath, "player output");
        var targetDirectory = scope.CreateDirectory("outside-output");
        var targetBuildReportPath = Path.Combine(targetDirectory, "build-report.json");
        WriteUtf8(
            targetBuildReportPath,
            IpcPayloadCodec.SerializeToElement(CreateBuildReportArtifact(paths)).GetRawText());
        var reportDirectory = Path.Combine(paths.RunnerOutputDirectory, "reports");
        Directory.CreateDirectory(reportDirectory);
        var linkPath = Path.Combine(reportDirectory, "linked");
        if (!TryCreateDirectorySymbolicLink(linkPath, targetDirectory))
        {
            return;
        }

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(
                paths,
                BuildReportSourceEntry.FromRunnerOutputRelativePath("reports/linked/build-report.json"),
                outputSourcePath),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
        Assert.Contains("reparse point", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenRunnerOutputRootIsSymbolicLink_ReturnsBuildReportMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "build-report-symlink-root");
        using var targetScope = TestDirectories.CreateTempScope("build-artifact-store", "build-report-symlink-root-target");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var targetOutputRoot = targetScope.CreateDirectory("output");
        WriteUtf8(
            Path.Combine(targetOutputRoot, "reports", "build-report.json"),
            IpcPayloadCodec.SerializeToElement(CreateBuildReportArtifact(paths)).GetRawText());
        Directory.Delete(paths.RunnerOutputDirectory);
        if (!TryCreateDirectorySymbolicLink(paths.RunnerOutputDirectory, targetOutputRoot))
        {
            return;
        }

        var writeResult = await store.AccountArtifactsAsync(
            CreateBuildReportOnlyAccountingRequest(paths, "reports/build-report.json"),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
        Assert.Contains("reparse point", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenBuildReportSourceIsMissing_ReturnsBuildReportMissingWithNotFoundMessage ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "build-report-source-missing");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var outputSourcePath = Path.Combine(paths.RunnerOutputDirectory, "build");
        WriteUtf8(outputSourcePath, "player output");

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(
                paths,
                BuildReportSourceEntry.FromRunnerOutputRelativePath("reports/missing-build-report.json"),
                outputSourcePath),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
        Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenBuildReportSourceIsFifo_ReturnsBuildReportMissing ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "build-report-source-fifo");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var buildReportPath = Path.Combine(paths.RunnerOutputDirectory, "reports", "build-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(buildReportPath)!);
        if (!TryCreateFifo(buildReportPath))
        {
            return;
        }

        var writeResult = await store.AccountArtifactsAsync(
            CreateBuildReportOnlyAccountingRequest(paths, "reports/build-report.json"),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
        Assert.Contains("regular file", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenOutputContainsFifo_ReturnsOutputManifestFailed ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-fifo");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var fifoPath = Path.Combine(paths.RunnerOutputDirectory, "build");
        if (!TryCreateFifo(fifoPath))
        {
            return;
        }

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        Assert.Contains("regular file", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenOutputFileIsUnreadable_ReturnsOutputManifestFailed ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-unreadable-file");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var outputPath = Path.Combine(paths.RunnerOutputDirectory, "build");
        WriteUtf8(outputPath, "secret");
        var originalMode = File.GetUnixFileMode(outputPath);
        try
        {
            File.SetUnixFileMode(outputPath, UnixFileMode.UserWrite);
            if (CanOpenForRead(outputPath))
            {
                return;
            }

            var writeResult = await store.AccountArtifactsAsync(
                CreateAccountingRequest(paths),
                CancellationToken.None);

            Assert.False(writeResult.IsSuccess);
            var error = Assert.IsType<ExecutionError>(writeResult.Error);
            Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
            Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        }
        finally
        {
            File.SetUnixFileMode(outputPath, originalMode);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenOutputDirectoryIsNonTraversable_ReturnsOutputManifestFailed ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-non-traversable-directory");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var blockedDirectory = Path.Combine(paths.RunnerOutputDirectory, "build", "blocked");
        var outputPath = Path.Combine(blockedDirectory, "payload.txt");
        WriteUtf8(outputPath, "secret");
        var originalMode = File.GetUnixFileMode(blockedDirectory);
        try
        {
            File.SetUnixFileMode(blockedDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            if (CanEnumerateDirectory(blockedDirectory))
            {
                return;
            }

            var writeResult = await store.AccountArtifactsAsync(
                CreateAccountingRequest(paths),
                CancellationToken.None);

            Assert.False(writeResult.IsSuccess);
            var error = Assert.IsType<ExecutionError>(writeResult.Error);
            Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
            Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        }
        finally
        {
            File.SetUnixFileMode(blockedDirectory, originalMode);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenOutputPathContainsBackslashTraversalText_ReturnsOutputManifestFailed ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-backslash-traversal");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var outputSourceDirectory = Path.Combine(paths.RunnerOutputDirectory, "build");
        Directory.CreateDirectory(outputSourceDirectory);
        File.WriteAllText(Path.Combine(outputSourceDirectory, "foo\\..\\..\\outside"), "ambiguous");

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        Assert.Contains("escaped", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenOutputSourceResolvesInsideArtifactRoot_ReturnsOutputPathInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "source-inside-artifact-root");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        WriteUnityGeneratedArtifacts(paths);
        var request = CreateAccountingRequest(paths) with
        {
            OutputSources = [BuildOutputSourceEntry.FromAbsolutePath(Path.Combine(paths.ArtifactsDirectory, "source"))],
        };

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputPathInvalid, error.Code);
        Assert.Contains("output path", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenOutputSourceResolvesOutsideRunnerOutputRoot_ReturnsOutputPathInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "source-outside-runner-output-root");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        WriteUnityGeneratedArtifacts(paths);
        var outsideSourcePath = scope.WriteFile("external-output.bin", "external");
        var request = CreateAccountingRequest(paths) with
        {
            OutputSources = [BuildOutputSourceEntry.FromAbsolutePath(outsideSourcePath)],
        };

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputPathInvalid, error.Code);
        Assert.Contains("runner output root", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenSuccessfulOutputSourceIsMissing_ReturnsOutputManifestFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "missing-success-output");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        WriteUnityGeneratedArtifacts(paths);

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenEmptyManifestIsAllowedAndSourceIsMissing_WritesEmptyManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "empty-output-manifest");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        WriteUnityGeneratedArtifacts(paths);
        var request = CreateAccountingRequest(paths) with
        {
            AllowEmptyOutputManifest = true,
        };

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        var result = Assert.IsType<BuildRunArtifactAccountingResult>(writeResult.Result);
        Assert.Equal(0, result.OutputManifest.EntryCount);
        Assert.Equal(0, result.OutputManifest.FileCount);
        Assert.Equal(0, result.OutputManifest.TotalBytes);
        using var outputManifest = JsonDocument.Parse(await File.ReadAllTextAsync(paths.OutputManifestJsonPath, CancellationToken.None));
        var outputRoot = outputManifest.RootElement;
        Assert.Equal(0, outputRoot.GetProperty("entries").GetArrayLength());
        Assert.Equal(0, outputRoot.GetProperty("files").GetArrayLength());
        Assert.Equal(0, outputRoot.GetProperty("entryCount").GetInt32());
        Assert.Equal(0, outputRoot.GetProperty("fileCount").GetInt32());
        Assert.Equal(0, outputRoot.GetProperty("totalBytes").GetInt64());
        var recalculatedManifestDigest = new BuildOutputManifestJsonContractWriter().CalculateManifestDigest(
            ReadOutputManifestContent(outputRoot));
        Assert.Equal(recalculatedManifestDigest, result.OutputManifest.ManifestDigest);
        await AssertFileSha256Async(paths.OutputManifestJsonPath, result.BuildOutputManifest.Digest);
    }

    [Theory]
    [InlineData("buildJson")]
    [InlineData("buildReport")]
    [InlineData("buildLog")]
    [InlineData("outputManifest")]
    [InlineData("artifactOutput")]
    [InlineData("runnerOutput")]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenArtifactPathEscapesLayout_ReturnsInvalidArgumentWithoutWriting (string pathKind)
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", $"path-escape-{pathKind}");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var escapedPath = pathKind is "artifactOutput" or "runnerOutput"
            ? scope.CreateDirectory("escaped-output")
            : Path.Combine(scope.FullPath, $"escaped-{pathKind}.json");
        var request = CreateAccountingRequest(EscapeArtifactPath(paths, pathKind, escapedPath));

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        if (pathKind is not "artifactOutput" and not "runnerOutput")
        {
            Assert.False(File.Exists(escapedPath));
        }

        Assert.False(File.Exists(paths.BuildJsonPath));
        Assert.False(File.Exists(paths.BuildReportJsonPath));
        Assert.False(File.Exists(paths.BuildLogPath));
        Assert.False(File.Exists(paths.OutputManifestJsonPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenArtifactDirectoryUsesUnexpectedLayout_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "unexpected-layout");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var artifactsDirectory = scope.CreateDirectory("unexpected-artifacts");
        var request = CreateAccountingRequest(paths with
        {
            ArtifactsDirectory = artifactsDirectory,
            BuildJsonPath = Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildMetadataFileName),
            BuildReportJsonPath = Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildReportFileName),
            BuildLogPath = Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildLogFileName),
            OutputManifestJsonPath = Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildOutputManifestFileName),
            ArtifactOutputDirectory = Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildOutputDirectoryName),
        });

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    private static FileBuildRunArtifactStore CreateStore ()
    {
        return new FileBuildRunArtifactStore(
            new BuildOutputManifestJsonContractWriter(),
            new BuildRunMetadataDocumentWriter());
    }

    private static ResolvedUnityProjectContext CreateProject (TestDirectoryScope scope)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: scope.CreateDirectory("UnityProject"),
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static BuildRunArtifactAccountingRequest CreateAccountingRequest (
        BuildRunArtifactPaths paths,
        params string[] outputSourcePaths)
    {
        return CreateAccountingRequest(
            paths,
            BuildReportSourceEntry.FromArtifact(CreateBuildReportArtifact(paths)),
            outputSourcePaths);
    }

    private static BuildRunArtifactAccountingRequest CreateAccountingRequest (
        BuildRunArtifactPaths paths,
        BuildReportSourceEntry buildReportSource,
        params string[] outputSourcePaths)
    {
        var sourcePaths = outputSourcePaths.Length == 0
            ? [Path.Combine(paths.RunnerOutputDirectory, "build")]
            : outputSourcePaths;
        return new BuildRunArtifactAccountingRequest(
            paths,
            "standaloneLinux64",
            "StandaloneLinux64",
            buildReportSource,
            sourcePaths.Select(static path => BuildOutputSourceEntry.FromAbsolutePath(path)).ToArray(),
            AllowEmptyOutputManifest: false);
    }

    private static BuildRunArtifactAccountingRequest CreateBuildReportOnlyAccountingRequest (
        BuildRunArtifactPaths paths,
        string buildReportSourcePath)
    {
        return new BuildRunArtifactAccountingRequest(
            paths,
            "standaloneLinux64",
            "StandaloneLinux64",
            BuildReportSourceEntry.FromRunnerOutputRelativePath(buildReportSourcePath),
            Array.Empty<BuildOutputSourceEntry>(),
            AllowEmptyOutputManifest: true);
    }

    private static IpcBuildReportArtifact CreateBuildReportArtifact (BuildRunArtifactPaths paths)
    {
        return new IpcBuildReportArtifact(
            SchemaVersion: 1,
            Result: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            UnityBuildTarget: "StandaloneLinux64",
            OutputPath: Path.Combine(paths.RunnerOutputDirectory, "build"),
            DurationMilliseconds: 2500,
            TotalSizeBytes: 4096,
            ErrorCount: 0,
            WarningCount: 1,
            Steps:
            [
                new IpcBuildReportStep(
                    Name: "Build player",
                    DurationMilliseconds: 2500,
                    Depth: 0,
                    MessageCount: 1),
            ],
            Messages:
            [
                new IpcBuildReportMessage(
                    Type: "warning",
                    Content: "Sample warning"),
            ]);
    }

    private static BuildRunArtifactPaths EscapeArtifactPath (
        BuildRunArtifactPaths paths,
        string pathKind,
        string escapedPath)
    {
        return pathKind switch
        {
            "buildJson" => paths with
            {
                BuildJsonPath = escapedPath,
            },
            "buildReport" => paths with
            {
                BuildReportJsonPath = escapedPath,
            },
            "buildLog" => paths with
            {
                BuildLogPath = escapedPath,
            },
            "outputManifest" => paths with
            {
                OutputManifestJsonPath = escapedPath,
            },
            "artifactOutput" => paths with
            {
                ArtifactOutputDirectory = escapedPath,
            },
            "runnerOutput" => paths with
            {
                RunnerOutputDirectory = escapedPath,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(pathKind), pathKind, "Unknown artifact path kind."),
        };
    }

    private static void WriteUnityGeneratedArtifacts (BuildRunArtifactPaths paths)
    {
        WriteUtf8(paths.BuildReportJsonPath, "{\"result\":\"succeeded\"}\n");
        WriteUtf8(paths.BuildLogPath, "build log\n");
    }

    private static BuildRunMetadataDocument CreateMetadata (string runId)
    {
        return new BuildRunMetadataDocument(
            1,
            runId,
            ParseJsonElement("""{"path":"/repo/.ucli/build/player.json","digest":"profile-digest"}"""),
            ParseJsonElement("""{"inputKind":"explicit","target":{"stableName":"standaloneLinux64","unityBuildTarget":"StandaloneLinux64"},"scenes":{"source":"explicit","paths":["Assets/Scenes/Main.unity"]},"options":{"development":true}}"""),
            ParseJsonElement("""{"kind":"buildPipeline","method":null,"invocation":{"arguments":{},"environment":{"variables":[],"secrets":[]}},"outputLayout":{"shape":"file","locationPathName":"/repo/.ucli/local/fingerprints/fingerprint/work/build/run-1/output/player/Player"}}"""),
            ParseJsonElement("""{"source":"buildPipelineBuildReport","status":"succeeded","summary":{"durationMilliseconds":1,"errorCount":0,"warningCount":0},"diagnostics":[],"buildReportRef":"buildReport"}"""),
            ParseJsonElement("""{"state":"completed"}"""),
            ParseJsonElement("""{"compile":"42","domainReload":"7"}"""),
            ParseJsonElement("""{"result":"succeeded"}"""),
            ParseJsonElement("""{"buildLog":{"stream":"file"}}"""),
            ParseJsonElement("""{"mode":"forbid","coverage":"full","mutated":false,"beforeDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","afterDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","items":[]}"""));
    }

    private static JsonElement ParseJsonElement (string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static BuildOutputManifestContentJsonContract ReadOutputManifestContent (JsonElement root)
    {
        var targetElement = root.GetProperty("target");
        var entryElements = root.GetProperty("entries");
        var entries = new List<BuildOutputManifestEntryJsonContract>(entryElements.GetArrayLength());
        for (var i = 0; i < entryElements.GetArrayLength(); i++)
        {
            var entry = entryElements[i];
            entries.Add(new BuildOutputManifestEntryJsonContract(
                entry.GetProperty("id").GetString()!,
                entry.GetProperty("kind").GetString()!,
                entry.GetProperty("sourcePath").GetString()!));
        }

        var fileElements = root.GetProperty("files");
        var files = new List<BuildOutputManifestFileJsonContract>(fileElements.GetArrayLength());
        for (var i = 0; i < fileElements.GetArrayLength(); i++)
        {
            var file = fileElements[i];
            files.Add(new BuildOutputManifestFileJsonContract(
                file.GetProperty("entryId").GetString()!,
                file.GetProperty("logicalPath").GetString()!,
                file.GetProperty("sourcePath").GetString()!,
                file.GetProperty("artifactPath").GetString()!,
                file.GetProperty("sizeBytes").GetInt64(),
                file.GetProperty("sha256").GetString()!));
        }

        return new BuildOutputManifestContentJsonContract(
            root.GetProperty("schemaVersion").GetInt32(),
            new BuildOutputManifestTargetJsonContract(
                targetElement.GetProperty("stableName").GetString()!,
                targetElement.GetProperty("unityBuildTarget").GetString()!),
            entries,
            root.GetProperty("entryCount").GetInt32(),
            root.GetProperty("fileCount").GetInt32(),
            root.GetProperty("totalBytes").GetInt64(),
            files);
    }

    private static byte[] WriteUtf8 (
        string path,
        string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var directoryPath = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {path}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllBytes(path, bytes);
        return bytes;
    }

    private static void AssertArtifactRef (
        JsonElement artifact,
        string expectedPath,
        string expectedDigest)
    {
        Assert.Equal(expectedPath, artifact.GetProperty("path").GetString());
        Assert.Equal(expectedDigest, artifact.GetProperty("digest").GetString());
        AssertLowerSha256(expectedDigest);
    }

    private static string GetArtifactKey (BuildArtifactKind kind)
    {
        return ContractLiteralCodec.ToValue(kind);
    }

    private static void AssertLowerSha256 (string sha256)
    {
        Assert.Equal(64, sha256.Length);
        Assert.All(sha256, static c => Assert.True(
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'),
            $"Character '{c}' is not lowercase hexadecimal."));
    }

    private static async ValueTask AssertFileSha256Async (
        string path,
        string expectedDigest)
    {
        Assert.Equal(
            expectedDigest,
            Sha256LowerHex.Compute(await File.ReadAllBytesAsync(path, CancellationToken.None)));
    }

    private static string ToRepositoryRelativeSlashPath (
        string repositoryRoot,
        string path)
    {
        return PathStringNormalizer.ToSlashSeparated(Path.GetRelativePath(repositoryRoot, path));
    }

    private static bool TryCreateFileSymbolicLink (
        string symbolicLinkPath,
        string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(symbolicLinkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryCreateDirectorySymbolicLink (
        string symbolicLinkPath,
        string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(symbolicLinkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool CanOpenForRead (string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool CanEnumerateDirectory (string path)
    {
        try
        {
            _ = Directory.EnumerateFileSystemEntries(path).Any();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryCreateFifo (string path)
    {
        return MkFifo(path, Convert.ToUInt32("600", 8)) == 0;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "mkfifo")]
    private static extern int MkFifo (
        string path,
        uint mode);
}
