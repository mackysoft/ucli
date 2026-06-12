using System.Text;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Assurance.Build;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class FileBuildRunArtifactStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task PrepareAsync_CreatesBuildRunArtifactLayout ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare-layout");
        var store = CreateStore();
        var project = CreateProject(scope);

        var result = store.Prepare(project, "run-1");

        Assert.True(result.IsSuccess);
        var paths = Assert.IsType<BuildRunArtifactPaths>(result.Paths);
        Assert.True(Directory.Exists(paths.ArtifactsDirectory));
        Assert.True(Directory.Exists(paths.OutputDirectory));
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
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PrepareAsync_WhenOutputDirectoryAlreadyExists_ReturnsBuildArtifactWriteFailed ()
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
    public async Task WriteArtifactsAsync_WritesRequiredArtifactsAndManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "write-artifacts");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var configBytes = WriteUtf8(Path.Combine(paths.OutputDirectory, "Data", "config.json"), "{\"quality\":\"high\"}\n");
        var playerBytes = WriteUtf8(Path.Combine(paths.OutputDirectory, "Game.x86_64"), "player binary\n");

        var writeResult = await store.WriteArtifactsAsync(
            CreateWriteRequest(paths),
            CancellationToken.None);

        Assert.True(writeResult.IsSuccess);
        var result = Assert.IsType<BuildRunArtifactWriteResult>(writeResult.Result);
        Assert.Equal(BuildArtifactKeys.Build, result.Build.Key);
        Assert.Equal(BuildArtifactKeys.BuildReport, result.BuildReport.Key);
        Assert.Equal(BuildArtifactKeys.BuildOutputManifest, result.BuildOutputManifest.Key);
        Assert.Equal(BuildArtifactKeys.BuildLog, result.BuildLog.Key);
        AssertLowerSha256(result.Build.Sha256);

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
        Assert.Equal(
            ToRepositoryRelativeSlashPath(scope.FullPath, paths.OutputDirectory),
            outputRoot.GetProperty("outputRoot").GetString());
        Assert.Equal("standaloneLinux64", outputRoot.GetProperty("target").GetString());
        Assert.Equal(2, outputRoot.GetProperty("fileCount").GetInt32());
        Assert.Equal(configBytes.Length + playerBytes.Length, outputRoot.GetProperty("totalBytes").GetInt64());
        Assert.Equal(result.OutputManifest.ManifestDigest, outputRoot.GetProperty("manifestDigest").GetString());
        Assert.Equal(2, result.OutputManifest.FileCount);
        Assert.Equal(configBytes.Length + playerBytes.Length, result.OutputManifest.TotalBytes);
        AssertLowerSha256(result.OutputManifest.ManifestDigest);

        var files = outputRoot.GetProperty("files");
        Assert.Equal("Data/config.json", files[0].GetProperty("path").GetString());
        Assert.Equal(configBytes.Length, files[0].GetProperty("sizeBytes").GetInt64());
        Assert.Equal(Sha256LowerHex.Compute(configBytes), files[0].GetProperty("sha256").GetString());
        Assert.Equal("Game.x86_64", files[1].GetProperty("path").GetString());
        Assert.Equal(playerBytes.Length, files[1].GetProperty("sizeBytes").GetInt64());
        Assert.Equal(Sha256LowerHex.Compute(playerBytes), files[1].GetProperty("sha256").GetString());

        using var buildMetadata = JsonDocument.Parse(await File.ReadAllTextAsync(paths.BuildJsonPath, CancellationToken.None));
        var buildRoot = buildMetadata.RootElement;
        Assert.Equal(1, buildRoot.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("run-1", buildRoot.GetProperty("runId").GetString());
        Assert.Equal("ucliArtifact", buildRoot.GetProperty("profile").GetProperty("output").GetProperty("kind").GetString());
        Assert.Equal("standaloneLinux64", buildRoot.GetProperty("resolved").GetProperty("target").GetProperty("stableName").GetString());

        var artifacts = buildRoot.GetProperty("artifacts");
        Assert.Equal(
            [
                BuildArtifactKeys.BuildReport,
                BuildArtifactKeys.BuildOutputManifest,
                BuildArtifactKeys.BuildLog,
            ],
            artifacts.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.False(artifacts.TryGetProperty(BuildArtifactKeys.Build, out _));
        AssertArtifactRef(
            artifacts.GetProperty(BuildArtifactKeys.BuildReport),
            BuildArtifactKind.BuildReport,
            ToRepositoryRelativeSlashPath(scope.FullPath, paths.BuildReportJsonPath),
            result.BuildReport.Sha256);
        AssertArtifactRef(
            artifacts.GetProperty(BuildArtifactKeys.BuildOutputManifest),
            BuildArtifactKind.BuildOutputManifest,
            ToRepositoryRelativeSlashPath(scope.FullPath, paths.OutputManifestJsonPath),
            result.BuildOutputManifest.Sha256);
        AssertArtifactRef(
            artifacts.GetProperty(BuildArtifactKeys.BuildLog),
            BuildArtifactKind.BuildLog,
            ToRepositoryRelativeSlashPath(scope.FullPath, paths.BuildLogPath),
            result.BuildLog.Sha256);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteArtifactsAsync_WhenOutputContainsSymbolicLink_ReturnsOutputManifestFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-symlink");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var targetPath = scope.WriteFile("target.txt", "linked output");
        var linkPath = Path.Combine(paths.OutputDirectory, "linked.txt");
        if (!TryCreateFileSymbolicLink(linkPath, targetPath))
        {
            return;
        }

        var writeResult = await store.WriteArtifactsAsync(
            CreateWriteRequest(paths),
            CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        Assert.Contains("reparse point", error.Message, StringComparison.OrdinalIgnoreCase);
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

    private static BuildRunArtifactWriteRequest CreateWriteRequest (BuildRunArtifactPaths paths)
    {
        return new BuildRunArtifactWriteRequest(
            paths,
            CreateMetadata(paths.RunId),
            "{\"result\":\"succeeded\"}\n",
            "build log\n",
            "standaloneLinux64");
    }

    private static BuildRunMetadataDocument CreateMetadata (string runId)
    {
        return new BuildRunMetadataDocument(
            1,
            runId,
            ParseJsonElement("""{"digest":"profile-digest","output":{"kind":"ucliArtifact"}}"""),
            ParseJsonElement("""{"target":{"stableName":"standaloneLinux64"}}"""),
            ParseJsonElement("""{"state":"completed"}"""),
            ParseJsonElement("""{"compile":"42","domainReload":"7"}"""),
            ParseJsonElement("""{"result":"succeeded"}"""),
            ParseJsonElement("""{"buildLog":{"stream":"file"}}"""));
    }

    private static JsonElement ParseJsonElement (string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
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
        BuildArtifactKind expectedKind,
        string expectedPath,
        string expectedSha256)
    {
        Assert.Equal(ContractLiteralCodec.ToValue(expectedKind), artifact.GetProperty("kind").GetString());
        Assert.Equal(expectedPath, artifact.GetProperty("path").GetString());
        Assert.Equal(expectedSha256, artifact.GetProperty("sha256").GetString());
        AssertLowerSha256(expectedSha256);
    }

    private static void AssertLowerSha256 (string sha256)
    {
        Assert.Equal(64, sha256.Length);
        Assert.All(sha256, static c => Assert.True(
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'),
            $"Character '{c}' is not lowercase hexadecimal."));
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
}
