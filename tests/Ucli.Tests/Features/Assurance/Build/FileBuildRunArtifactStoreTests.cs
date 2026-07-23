using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Assurance.Build;
using static MackySoft.Ucli.Tests.Features.Assurance.Build.FileBuildRunArtifactStoreTestSupport;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class FileBuildRunArtifactStoreTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_ThenWriteMetadataAsync_WritesRequiredArtifactsAndManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "write-artifacts");
        var (store, paths) = PrepareArtifacts(scope);
        var directorySourcePath = Path.Combine(paths.RunnerOutputDirectory.Value, "player");
        var fileSourcePath = Path.Combine(paths.RunnerOutputDirectory.Value, "Game.x86_64");
        var zConfigSourcePath = Path.Combine(directorySourcePath, "Data", "z-config.json");
        var aConfigSourcePath = Path.Combine(directorySourcePath, "Data", "a-config.json");
        var zConfigBytes = WriteUtf8(zConfigSourcePath, "{\"quality\":\"high\"}\n");
        var aConfigBytes = WriteUtf8(aConfigSourcePath, "{\"quality\":\"low\"}\n");
        var playerBytes = WriteUtf8(fileSourcePath, "player binary\n");
        var buildLogBytes = WriteUtf8(paths.BuildLogPath.Value, "build log\n");

        var accountingOperation = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths, directorySourcePath, fileSourcePath),
            CancellationToken.None);

        Assert.True(accountingOperation.IsSuccess);
        var result = Assert.IsType<BuildRunArtifactAccountingResult>(accountingOperation.Result);
        Assert.NotNull(result.BuildReport);
        Assert.Equal(BuildArtifactKind.BuildReport, result.BuildReport!.Kind);
        Assert.Equal(BuildArtifactKind.BuildOutputManifest, result.BuildOutputManifest.Kind);
        Assert.Equal(BuildArtifactKind.BuildLog, result.BuildLog.Kind);
        Assert.Equal(Sha256Digest.Compute(buildLogBytes), result.BuildLog.Digest);
        var buildReportBytes = await File.ReadAllBytesAsync(paths.BuildReportJsonPath.Value, CancellationToken.None);
        Assert.Equal(Sha256Digest.Compute(buildReportBytes), result.BuildReport.Digest);

        var metadataWriteResult = await store.WriteMetadataAsync(
            new BuildRunMetadataWriteRequest(
                paths,
                CreateMetadata(paths.RunId, paths.RunnerOutputDirectory.Value),
                result),
            CancellationToken.None);

        Assert.True(metadataWriteResult.IsSuccess);
        var buildRef = Assert.IsType<BuildArtifactRef>(metadataWriteResult.Artifact);
        Assert.Equal(BuildArtifactKind.Build, buildRef.Kind);
        AssertLowerSha256(buildRef.Digest);

        var topLevelArtifactNames = Directory
            .EnumerateFileSystemEntries(paths.ArtifactsDirectory.Value)
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

        using var outputManifest = JsonDocument.Parse(await File.ReadAllTextAsync(paths.OutputManifestJsonPath.Value, CancellationToken.None));
        var outputRoot = outputManifest.RootElement;
        var target = outputRoot.GetProperty("target");
        Assert.Equal("standaloneLinux64", target.GetProperty("stableName").GetString());
        Assert.Equal("StandaloneLinux64", target.GetProperty("unityBuildTarget").GetString());
        var entries = outputRoot.GetProperty("entries");
        Assert.Equal(2, entries.GetArrayLength());
        Assert.Equal("output-0001", entries[0].GetProperty("id").GetString());
        Assert.Equal("directory", entries[0].GetProperty("kind").GetString());
        Assert.Equal(AbsolutePath.Parse(directorySourcePath).Value, entries[0].GetProperty("sourcePath").GetString());
        Assert.Equal("output-0002", entries[1].GetProperty("id").GetString());
        Assert.Equal("file", entries[1].GetProperty("kind").GetString());
        Assert.Equal(AbsolutePath.Parse(fileSourcePath).Value, entries[1].GetProperty("sourcePath").GetString());
        Assert.Equal(2, outputRoot.GetProperty("entryCount").GetInt32());
        Assert.Equal(3, outputRoot.GetProperty("fileCount").GetInt32());
        Assert.Equal(aConfigBytes.Length + zConfigBytes.Length + playerBytes.Length, outputRoot.GetProperty("totalBytes").GetInt64());
        Assert.Equal(result.OutputManifest.ManifestDigest.ToString(), outputRoot.GetProperty("manifestDigest").GetString());
        Assert.Equal(2, result.OutputManifest.EntryCount);
        Assert.Equal(3, result.OutputManifest.FileCount);
        Assert.Equal(aConfigBytes.Length + zConfigBytes.Length + playerBytes.Length, result.OutputManifest.TotalBytes);
        AssertLowerSha256(result.OutputManifest.ManifestDigest);

        var files = outputRoot.GetProperty("files");
        Assert.Equal("output-0001", files[0].GetProperty("entryId").GetString());
        Assert.Equal("output-0001/Data/a-config.json", files[0].GetProperty("logicalPath").GetString());
        Assert.Equal(AbsolutePath.Parse(aConfigSourcePath).Value, files[0].GetProperty("sourcePath").GetString());
        Assert.Equal("output/output-0001/Data/a-config.json", files[0].GetProperty("artifactPath").GetString());
        Assert.Equal(aConfigBytes.Length, files[0].GetProperty("sizeBytes").GetInt64());
        Assert.Equal(Sha256LowerHex.Compute(aConfigBytes), files[0].GetProperty("sha256").GetString());
        Assert.Equal("output-0001", files[1].GetProperty("entryId").GetString());
        Assert.Equal("output-0001/Data/z-config.json", files[1].GetProperty("logicalPath").GetString());
        Assert.Equal(AbsolutePath.Parse(zConfigSourcePath).Value, files[1].GetProperty("sourcePath").GetString());
        Assert.Equal("output/output-0001/Data/z-config.json", files[1].GetProperty("artifactPath").GetString());
        Assert.Equal(zConfigBytes.Length, files[1].GetProperty("sizeBytes").GetInt64());
        Assert.Equal(Sha256LowerHex.Compute(zConfigBytes), files[1].GetProperty("sha256").GetString());
        Assert.Equal("output-0002", files[2].GetProperty("entryId").GetString());
        Assert.Equal("output-0002/Game.x86_64", files[2].GetProperty("logicalPath").GetString());
        Assert.Equal(AbsolutePath.Parse(fileSourcePath).Value, files[2].GetProperty("sourcePath").GetString());
        Assert.Equal("output/output-0002/Game.x86_64", files[2].GetProperty("artifactPath").GetString());
        Assert.Equal(playerBytes.Length, files[2].GetProperty("sizeBytes").GetInt64());
        Assert.Equal(Sha256LowerHex.Compute(playerBytes), files[2].GetProperty("sha256").GetString());
        await AssertFileSha256Async(
            Path.Combine(paths.ArtifactOutputDirectory.Value, "output-0001", "Data", "a-config.json"),
            Sha256Digest.Parse(files[0].GetProperty("sha256").GetString()!));
        await AssertFileSha256Async(
            Path.Combine(paths.ArtifactOutputDirectory.Value, "output-0001", "Data", "z-config.json"),
            Sha256Digest.Parse(files[1].GetProperty("sha256").GetString()!));
        await AssertFileSha256Async(
            Path.Combine(paths.ArtifactOutputDirectory.Value, "output-0002", "Game.x86_64"),
            Sha256Digest.Parse(files[2].GetProperty("sha256").GetString()!));
        var recalculatedManifestDigest = new BuildOutputManifestJsonContractWriter().CalculateManifestDigest(
            BuildOutputManifestJsonContractTestSupport.ReadContent(outputRoot));
        Assert.Equal(recalculatedManifestDigest, result.OutputManifest.ManifestDigest);
        Assert.NotEqual(recalculatedManifestDigest, result.BuildOutputManifest.Digest);
        await AssertFileSha256Async(paths.OutputManifestJsonPath.Value, result.BuildOutputManifest.Digest);

        using var buildMetadata = JsonDocument.Parse(await File.ReadAllTextAsync(paths.BuildJsonPath.Value, CancellationToken.None));
        var buildRoot = buildMetadata.RootElement;
        Assert.Equal(1, buildRoot.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(RunIdTestValues.BuildText, buildRoot.GetProperty("runId").GetString());
        Assert.False(buildRoot.TryGetProperty("project", out _));
        Assert.False(buildRoot.TryGetProperty("input", out _));
        Assert.False(buildRoot.TryGetProperty("output", out _));
        Assert.False(buildRoot.TryGetProperty("dirtyState", out _));
        Assert.False(buildRoot.GetProperty("profile").TryGetProperty("output", out _));
        Assert.Equal("buildPipeline", buildRoot.GetProperty("runner").GetProperty("kind").GetString());
        Assert.Equal("file", buildRoot.GetProperty("runner").GetProperty("outputLayout").GetProperty("shape").GetString());
        Assert.Equal(
            Path.Combine(paths.RunnerOutputDirectory.Value, "player", "Player"),
            buildRoot.GetProperty("runner").GetProperty("outputLayout").GetProperty("locationPathName").GetString());
        var inputs = buildRoot.GetProperty("inputs");
        Assert.Equal("explicit", inputs.GetProperty("inputKind").GetString());
        Assert.Equal("standaloneLinux64", inputs.GetProperty("target").GetProperty("stableName").GetString());
        Assert.Equal("StandaloneLinux64", inputs.GetProperty("target").GetProperty("unityBuildTarget").GetString());

        var artifacts = buildRoot.GetProperty("artifacts");
        Assert.Equal(
            [
                ContractLiteralCodec.ToValue(BuildArtifactKind.BuildReport),
                ContractLiteralCodec.ToValue(BuildArtifactKind.BuildOutputManifest),
                ContractLiteralCodec.ToValue(BuildArtifactKind.BuildLog),
            ],
            artifacts.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.False(artifacts.TryGetProperty(ContractLiteralCodec.ToValue(BuildArtifactKind.Build), out _));
        AssertArtifactRef(
            artifacts.GetProperty(ContractLiteralCodec.ToValue(BuildArtifactKind.BuildReport)),
            UcliStoragePathNames.BuildReportFileName,
            result.BuildReport.Digest);
        AssertArtifactRef(
            artifacts.GetProperty(ContractLiteralCodec.ToValue(BuildArtifactKind.BuildOutputManifest)),
            UcliStoragePathNames.BuildOutputManifestFileName,
            result.BuildOutputManifest.Digest);
        AssertArtifactRef(
            artifacts.GetProperty(ContractLiteralCodec.ToValue(BuildArtifactKind.BuildLog)),
            UcliStoragePathNames.BuildLogFileName,
            result.BuildLog.Digest);
        await AssertFileSha256Async(paths.BuildJsonPath.Value, buildRef.Digest);
        await AssertFileSha256Async(paths.BuildReportJsonPath.Value, result.BuildReport.Digest);
        await AssertFileSha256Async(paths.BuildLogPath.Value, result.BuildLog.Digest);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenBuildReportSourceIsMissing_ReturnsBuildReportMissingWithNotFoundMessage ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "build-report-source-missing");
        var (store, paths) = PrepareArtifacts(scope);
        var outputSourcePath = Path.Combine(paths.RunnerOutputDirectory.Value, "build");
        WriteUtf8(outputSourcePath, "player output");

        var writeResult = await store.AccountArtifactsAsync(
            CreateAccountingRequest(
                paths,
                BuildReportSourceEntry.FromRunnerOutputRelativePath(
                    new BuildRunnerOutputPath("reports/missing-build-report.json")),
                outputSourcePath),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
        Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenBuildReportSourceViolatesNormalizedContract_ReturnsBuildReportMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "invalid-build-report-contract");
        var (store, paths) = PrepareArtifacts(scope);
        const string sourceRelativePath = "reports/build-report.json";
        WriteUtf8(
            Path.Combine(paths.RunnerOutputDirectory.Value, sourceRelativePath),
            """
            {
              "schemaVersion": 1,
              "result": "succeeded",
              "unityBuildTarget": "StandaloneLinux64",
              "outputPath": "",
              "durationMilliseconds": 0,
              "totalSizeBytes": 0,
              "errorCount": 0,
              "warningCount": 0,
              "steps": [{ "name": "Build player", "durationMilliseconds": -1, "depth": 0, "messageCount": 0 }],
              "messages": []
            }
            """);

        var result = await store.AccountArtifactsAsync(
            CreateBuildReportOnlyAccountingRequest(paths, sourceRelativePath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenSuccessfulOutputSourceIsMissing_ReturnsOutputManifestFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "missing-success-output");
        var (store, paths) = PrepareArtifacts(scope);
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
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenEmptyManifestIsAllowedAndSourceIsMissing_WritesEmptyManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "empty-output-manifest");
        var (store, paths) = PrepareArtifacts(scope);
        WriteUnityGeneratedArtifacts(paths);
        var defaultRequest = CreateAccountingRequest(paths);
        var request = new BuildRunArtifactAccountingRequest(
            defaultRequest.Paths,
            defaultRequest.BuildTarget,
            defaultRequest.UnityBuildTarget,
            defaultRequest.BuildReport,
            defaultRequest.OutputSources,
            allowEmptyOutputManifest: true);

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        var result = Assert.IsType<BuildRunArtifactAccountingResult>(writeResult.Result);
        Assert.Equal(0, result.OutputManifest.EntryCount);
        Assert.Equal(0, result.OutputManifest.FileCount);
        Assert.Equal(0, result.OutputManifest.TotalBytes);
        using var outputManifest = JsonDocument.Parse(await File.ReadAllTextAsync(paths.OutputManifestJsonPath.Value, CancellationToken.None));
        var outputRoot = outputManifest.RootElement;
        Assert.Equal(0, outputRoot.GetProperty("entries").GetArrayLength());
        Assert.Equal(0, outputRoot.GetProperty("files").GetArrayLength());
        Assert.Equal(0, outputRoot.GetProperty("entryCount").GetInt32());
        Assert.Equal(0, outputRoot.GetProperty("fileCount").GetInt32());
        Assert.Equal(0, outputRoot.GetProperty("totalBytes").GetInt64());
        var recalculatedManifestDigest = new BuildOutputManifestJsonContractWriter().CalculateManifestDigest(
            BuildOutputManifestJsonContractTestSupport.ReadContent(outputRoot));
        Assert.Equal(recalculatedManifestDigest, result.OutputManifest.ManifestDigest);
        await AssertFileSha256Async(paths.OutputManifestJsonPath.Value, result.BuildOutputManifest.Digest);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteMetadataAsync_WithMismatchedRunId_ReturnsInvalidArgumentWithoutWritingMetadata ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "metadata-run-id-mismatch");
        var (store, paths) = PrepareArtifacts(scope);
        var accounting = new BuildRunArtifactAccountingResult(
            BuildReport: null,
            BuildOutputManifest: new BuildArtifactRef(BuildArtifactKind.BuildOutputManifest, "output-manifest.json", Sha256Digest.Parse(new string('a', 64))),
            BuildLog: new BuildArtifactRef(BuildArtifactKind.BuildLog, "build.log", Sha256Digest.Parse(new string('b', 64))),
            OutputManifest: new BuildOutputManifestSummary(Sha256Digest.Parse(new string('c', 64)), 0, 0, 0));

        var result = await store.WriteMetadataAsync(
            new BuildRunMetadataWriteRequest(
                paths,
                CreateMetadata(Guid.NewGuid(), paths.RunnerOutputDirectory.Value),
                accounting),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.False(File.Exists(paths.BuildJsonPath.Value));
    }

    private static BuildRunMetadataDocument CreateMetadata (
        Guid runId,
        string runnerOutputDirectory)
    {
        var runner = ParseJsonElement(JsonSerializer.Serialize(new
        {
            kind = "buildPipeline",
            method = (string?)null,
            invocation = new
            {
                arguments = new Dictionary<string, string>(),
                environment = new
                {
                    variables = Array.Empty<string>(),
                    secrets = Array.Empty<string>(),
                },
            },
            outputLayout = new
            {
                shape = "file",
                locationPathName = Path.Combine(runnerOutputDirectory, "player", "Player"),
            },
        }));
        return new BuildRunMetadataDocument(
            1,
            runId,
            ParseJsonElement("""{"path":"/repo/.ucli/build/player.json","digest":"profile-digest"}"""),
            ParseJsonElement("""{"inputKind":"explicit","target":{"stableName":"standaloneLinux64","unityBuildTarget":"StandaloneLinux64"},"scenes":{"source":"explicit","paths":["Assets/Scenes/Main.unity"]},"options":{"development":true}}"""),
            runner,
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

    private static void AssertArtifactRef (
        JsonElement artifact,
        string expectedPath,
        Sha256Digest expectedDigest)
    {
        Assert.Equal(expectedPath, artifact.GetProperty("path").GetString());
        Assert.Equal(expectedDigest.ToString(), artifact.GetProperty("digest").GetString());
        AssertLowerSha256(expectedDigest);
    }

    private static void AssertLowerSha256 (Sha256Digest sha256)
    {
        var value = sha256.ToString();
        Assert.Equal(64, value.Length);
        Assert.All(value, static c => Assert.True(
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'),
            $"Character '{c}' is not lowercase hexadecimal."));
    }

    private static async ValueTask AssertFileSha256Async (
        string path,
        Sha256Digest expectedDigest)
    {
        Assert.Equal(
            expectedDigest,
            Sha256Digest.Compute(await File.ReadAllBytesAsync(path, CancellationToken.None)));
    }

}
