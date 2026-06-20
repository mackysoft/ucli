using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
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
    public async Task AccountArtifactsAsync_ThenWriteMetadataAsync_WritesRequiredArtifactsAndManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "write-artifacts");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var configBytes = WriteUtf8(Path.Combine(paths.OutputDirectory, "Data", "config.json"), "{\"quality\":\"high\"}\n");
        var playerBytes = WriteUtf8(Path.Combine(paths.OutputDirectory, "Game.x86_64"), "player binary\n");
        var buildReportBytes = WriteUtf8(paths.BuildReportJsonPath, "{\"result\":\"succeeded\"}\n");
        var buildLogBytes = WriteUtf8(paths.BuildLogPath, "build log\n");

        var accountingOperation = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths),
            CancellationToken.None);

        Assert.True(accountingOperation.IsSuccess);
        var result = Assert.IsType<BuildRunArtifactAccountingResult>(accountingOperation.Result);
        Assert.Equal(BuildArtifactKind.BuildReport, result.BuildReport.Kind);
        Assert.Equal(BuildArtifactKind.BuildOutputManifest, result.BuildOutputManifest.Kind);
        Assert.Equal(BuildArtifactKind.BuildLog, result.BuildLog.Kind);
        Assert.Equal(Sha256LowerHex.Compute(buildReportBytes), result.BuildReport.Digest);
        Assert.Equal(Sha256LowerHex.Compute(buildLogBytes), result.BuildLog.Digest);

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
        Assert.Equal(
            ToRepositoryRelativeSlashPath(scope.FullPath, paths.OutputDirectory),
            outputRoot.GetProperty("outputRoot").GetString());
        Assert.Equal("standaloneLinux64", outputRoot.GetProperty("buildTarget").GetString());
        Assert.Equal(2, outputRoot.GetProperty("fileCount").GetInt32());
        Assert.Equal(configBytes.Length + playerBytes.Length, outputRoot.GetProperty("totalBytes").GetInt64());
        Assert.Equal(result.OutputManifest.ManifestDigest, outputRoot.GetProperty("manifestDigest").GetString());
        Assert.Equal(2, result.OutputManifest.EntryCount);
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
        var recalculatedManifestDigest = new BuildOutputManifestJsonContractWriter().CalculateManifestDigest(
            ReadOutputManifestContent(outputRoot));
        Assert.Equal(recalculatedManifestDigest, result.OutputManifest.ManifestDigest);
        Assert.NotEqual(recalculatedManifestDigest, result.BuildOutputManifest.Digest);
        await AssertFileSha256Async(paths.OutputManifestJsonPath, result.BuildOutputManifest.Digest);

        using var buildMetadata = JsonDocument.Parse(await File.ReadAllTextAsync(paths.BuildJsonPath, CancellationToken.None));
        var buildRoot = buildMetadata.RootElement;
        Assert.Equal(1, buildRoot.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("run-1", buildRoot.GetProperty("runId").GetString());
        Assert.False(buildRoot.GetProperty("profile").TryGetProperty("output", out _));
        Assert.False(buildRoot.GetProperty("output").TryGetProperty("kind", out _));
        Assert.False(buildRoot.GetProperty("output").TryGetProperty("artifactRoot", out _));
        Assert.False(buildRoot.GetProperty("output").TryGetProperty("outputRoot", out _));
        Assert.Equal("standaloneLinux64", buildRoot.GetProperty("input").GetProperty("buildTarget").GetProperty("stableName").GetString());

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
            ToRepositoryRelativeSlashPath(scope.FullPath, paths.BuildReportJsonPath),
            result.BuildReport.Digest);
        AssertArtifactRef(
            artifacts.GetProperty(GetArtifactKey(BuildArtifactKind.BuildOutputManifest)),
            ToRepositoryRelativeSlashPath(scope.FullPath, paths.OutputManifestJsonPath),
            result.BuildOutputManifest.Digest);
        AssertArtifactRef(
            artifacts.GetProperty(GetArtifactKey(BuildArtifactKind.BuildLog)),
            ToRepositoryRelativeSlashPath(scope.FullPath, paths.BuildLogPath),
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
        var linkPath = Path.Combine(paths.OutputDirectory, "linked.txt");
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
        var fifoPath = Path.Combine(paths.OutputDirectory, "pipe");
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
        File.WriteAllText(Path.Combine(paths.OutputDirectory, "foo\\..\\..\\outside"), "ambiguous");

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
    public async Task AccountArtifactsAsync_WhenReportedOutputPathEscapesOutputDirectory_ReturnsOutputManifestFailed ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "reported-output-escape");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        WriteUnityGeneratedArtifacts(paths);
        var request = CreateAccountingRequest(paths) with
        {
            ReportedOutputPath = Path.Combine(scope.FullPath, "outside", "build"),
        };

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, error.Code);
        Assert.Contains("output path", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("buildJson")]
    [InlineData("buildReport")]
    [InlineData("buildLog")]
    [InlineData("outputManifest")]
    [InlineData("output")]
    [Trait("Size", "Small")]
    public async Task AccountArtifactsAsync_WhenArtifactPathEscapesLayout_ReturnsInvalidArgumentWithoutWriting (string pathKind)
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", $"path-escape-{pathKind}");
        var store = CreateStore();
        var project = CreateProject(scope);
        var prepareResult = store.Prepare(project, "run-1");
        var paths = Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths);
        var escapedPath = pathKind == "output"
            ? scope.CreateDirectory("escaped-output")
            : Path.Combine(scope.FullPath, $"escaped-{pathKind}.json");
        var request = CreateAccountingRequest(EscapeArtifactPath(paths, pathKind, escapedPath));

        var writeResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(writeResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(writeResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        if (pathKind != "output")
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
            OutputDirectory = Path.Combine(artifactsDirectory, UcliStoragePathNames.BuildOutputDirectoryName),
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

    private static BuildRunArtifactAccountingRequest CreateAccountingRequest (BuildRunArtifactPaths paths)
    {
        return new BuildRunArtifactAccountingRequest(
            paths,
            Path.Combine(paths.OutputDirectory, "build"),
            "standaloneLinux64");
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
            "output" => paths with
            {
                OutputDirectory = escapedPath,
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
            ParseJsonElement("""{"projectPath":"/repo/UnityProject","projectFingerprint":"fingerprint","unityVersion":"6000.1.4f1"}"""),
            ParseJsonElement("""{"path":"/repo/.ucli/build/player.json","digest":"profile-digest"}"""),
            ParseJsonElement("""{"buildTarget":{"stableName":"standaloneLinux64"}}"""),
            ParseJsonElement("""{"state":"completed"}"""),
            ParseJsonElement("""{"compile":"42","domainReload":"7"}"""),
            ParseJsonElement("""{"result":"succeeded"}"""),
            ParseJsonElement("""{"buildLog":{"stream":"file"}}"""),
            ParseJsonElement("""{"manifestDigest":"manifest-digest"}"""),
            ParseJsonElement("""{"checked":true,"dirty":false,"items":[]}"""));
    }

    private static JsonElement ParseJsonElement (string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static BuildOutputManifestContentJsonContract ReadOutputManifestContent (JsonElement root)
    {
        var fileElements = root.GetProperty("files");
        var files = new List<BuildOutputManifestFileJsonContract>(fileElements.GetArrayLength());
        for (var i = 0; i < fileElements.GetArrayLength(); i++)
        {
            var file = fileElements[i];
            files.Add(new BuildOutputManifestFileJsonContract(
                file.GetProperty("path").GetString()!,
                file.GetProperty("sizeBytes").GetInt64(),
                file.GetProperty("sha256").GetString()!));
        }

        return new BuildOutputManifestContentJsonContract(
            root.GetProperty("schemaVersion").GetInt32(),
            root.GetProperty("outputRoot").GetString()!,
            root.GetProperty("buildTarget").GetString()!,
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

    private static bool TryCreateFifo (string path)
    {
        return MkFifo(path, Convert.ToUInt32("600", 8)) == 0;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "mkfifo")]
    private static extern int MkFifo (
        string path,
        uint mode);
}
