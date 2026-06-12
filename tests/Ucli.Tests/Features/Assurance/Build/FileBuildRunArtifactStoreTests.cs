using System.Text;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Assurance.Build;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class FileBuildRunArtifactStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task PrepareAsync_CreatesBuildArtifactLayoutAndRejectsExistingRunDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "prepare");
        var store = new FileBuildRunArtifactStore();
        var project = CreateProject(scope.FullPath);

        var result = await store.PrepareAsync(project, "run-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var paths = result.Paths!;
        var expectedRunDirectory = UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
            scope.FullPath,
            "fingerprint",
            "run-1");
        Assert.Equal(expectedRunDirectory, paths.RunDirectory);
        Assert.Equal(Path.Combine(expectedRunDirectory, UcliStoragePathNames.BuildMetadataFileName), paths.BuildJsonPath);
        Assert.Equal(Path.Combine(expectedRunDirectory, UcliStoragePathNames.BuildReportFileName), paths.BuildReportPath);
        Assert.Equal(Path.Combine(expectedRunDirectory, UcliStoragePathNames.BuildLogFileName), paths.BuildLogPath);
        Assert.Equal(Path.Combine(expectedRunDirectory, UcliStoragePathNames.BuildOutputManifestFileName), paths.OutputManifestPath);
        Assert.Equal(Path.Combine(expectedRunDirectory, UcliStoragePathNames.BuildOutputDirectoryName), paths.OutputDirectory);
        Assert.True(Directory.Exists(paths.RunDirectory));
        Assert.True(Directory.Exists(paths.OutputDirectory));

        var duplicateResult = await store.PrepareAsync(project, "run-1", CancellationToken.None);

        Assert.False(duplicateResult.IsSuccess);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, duplicateResult.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteOutputManifestAsync_SortsFilesAndWritesStableDigests ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "manifest");
        var store = new FileBuildRunArtifactStore();
        var prepareResult = await store.PrepareAsync(CreateProject(scope.FullPath), "run-1", CancellationToken.None);
        var paths = prepareResult.Paths!;
        WriteText(Path.Combine(paths.OutputDirectory, "zeta.txt"), "zeta");
        WriteText(Path.Combine(paths.OutputDirectory, "nested", "alpha.txt"), "alpha");

        var result = await store.WriteOutputManifestAsync(paths, "standaloneLinux64", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var manifest = result.Manifest!;
        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal(paths.OutputDirectory, manifest.OutputRoot);
        Assert.Equal("standaloneLinux64", manifest.Target);
        Assert.Equal(2, manifest.FileCount);
        Assert.Equal(9, manifest.TotalBytes);
        Assert.Equal(["nested/alpha.txt", "zeta.txt"], manifest.Files.Select(static file => file.Path).ToArray());
        Assert.Equal(5, manifest.Files[0].SizeBytes);
        Assert.Equal(Sha256LowerHex.Compute(Encoding.UTF8.GetBytes("alpha")), manifest.Files[0].Sha256);
        Assert.Equal(4, manifest.Files[1].SizeBytes);
        Assert.Equal(Sha256LowerHex.Compute(Encoding.UTF8.GetBytes("zeta")), manifest.Files[1].Sha256);
        var persistedJson = await File.ReadAllTextAsync(paths.OutputManifestPath, CancellationToken.None);
        Assert.Contains("manifestDigest", persistedJson, StringComparison.Ordinal);
        var persistedManifest = JsonSerializer.Deserialize<BuildOutputManifest>(persistedJson, IpcJsonSerializerOptions.Default);
        Assert.NotNull(persistedManifest);
        Assert.Equal(manifest.ManifestDigest, persistedManifest!.ManifestDigest);

        var digestResult = await store.CalculateDigestAsync(paths.OutputManifestPath, CancellationToken.None);
        var contentDigestResult = await store.CalculateOutputManifestContentDigestAsync(paths.OutputManifestPath, CancellationToken.None);

        Assert.True(digestResult.IsSuccess);
        Assert.True(contentDigestResult.IsSuccess);
        Assert.Equal(Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(persistedJson)), digestResult.Digest);
        Assert.Equal(manifest.ManifestDigest, contentDigestResult.Digest);
        Assert.NotEqual(manifest.ManifestDigest, digestResult.Digest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CalculateOutputManifestContentDigestAsync_WhenPersistedManifestDigestDiffers_ReturnsDigestMismatch ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "manifest-digest-mismatch");
        var store = new FileBuildRunArtifactStore();
        var prepareResult = await store.PrepareAsync(CreateProject(scope.FullPath), "run-1", CancellationToken.None);
        var paths = prepareResult.Paths!;

        var manifestResult = await store.WriteOutputManifestAsync(paths, "standaloneLinux64", CancellationToken.None);
        Assert.True(manifestResult.IsSuccess);
        var persistedJson = await File.ReadAllTextAsync(paths.OutputManifestPath, CancellationToken.None);
        await File.WriteAllTextAsync(
            paths.OutputManifestPath,
            persistedJson.Replace(manifestResult.Manifest!.ManifestDigest, new string('0', 64), StringComparison.Ordinal),
            CancellationToken.None);

        var result = await store.CalculateOutputManifestContentDigestAsync(paths.OutputManifestPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BuildErrorCodes.BuildOutputDigestMismatch, result.Error!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteOutputManifestAsync_WhenOutputRootIsFile_ReturnsManifestFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "manifest-root-file");
        var store = new FileBuildRunArtifactStore();
        var outputRootFile = scope.WriteFile("output-root", "not a directory");
        var paths = new BuildRunArtifactPaths(
            RunDirectory: scope.FullPath,
            BuildJsonPath: scope.GetPath("build.json"),
            BuildReportPath: scope.GetPath("build-report.json"),
            BuildLogPath: scope.GetPath("build.log"),
            OutputManifestPath: scope.GetPath("output-manifest.json"),
            OutputDirectory: outputRootFile);

        var result = await store.WriteOutputManifestAsync(paths, "standaloneLinux64", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, result.Error!.Code);
        Assert.False(File.Exists(paths.OutputManifestPath));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ContainsOutputPath_ReturnsTrueOnlyForPreparedOutputDirectory ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-path");
        var store = new FileBuildRunArtifactStore();
        var prepareResult = await store.PrepareAsync(CreateProject(scope.FullPath), "run-1", CancellationToken.None);
        var paths = prepareResult.Paths!;

        Assert.True(store.ContainsOutputPath(paths, paths.OutputDirectory));
        Assert.True(store.ContainsOutputPath(paths, Path.Combine(paths.OutputDirectory, "Game.x86_64")));
        Assert.False(store.ContainsOutputPath(paths, Path.Combine(paths.RunDirectory, "sibling")));
        Assert.False(store.ContainsOutputPath(paths, "relative-output"));
        Assert.False(store.ContainsOutputPath(paths, string.Empty));
    }

    private static ResolvedUnityProjectContext CreateProject (string repositoryRoot)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: Path.Combine(repositoryRoot, "UnityProject"),
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption,
            PathSourceLabel: "--projectPath",
            UnityVersion: "6000.1.4f1");
    }

    private static void WriteText (
        string path,
        string contents)
    {
        var directoryPath = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException($"Directory path could not be determined: {path}");
        }

        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
