using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using static MackySoft.Ucli.Tests.Features.Assurance.Build.FileBuildRunArtifactStoreTestSupport;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class FileBuildRunArtifactStorePublicationTests
{
    private const int LongRunningOutputBytes = 16 * 1024 * 1024;

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenLaterOutputSourceDisappears_DoesNotPublishPartialOutputAndCanRetry ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-publication-retry");
        var (store, paths) = PrepareArtifacts(scope);
        WriteUnityGeneratedArtifacts(paths);
        var firstSourcePath = Path.Combine(paths.RunnerOutputDirectory.Value, "first.bin");
        var laterSourcePath = Path.Combine(paths.RunnerOutputDirectory.Value, "later.bin");
        WriteLargeFile(firstSourcePath);
        WriteUtf8(laterSourcePath, "later output\n");
        var request = CreateAccountingRequest(paths, firstSourcePath, laterSourcePath);

        var accountingTask = store.AccountArtifactsAsync(request, CancellationToken.None).AsTask();
        await WaitForStagingAsync(paths, accountingTask);
        File.Delete(laterSourcePath);

        var failedResult = await accountingTask;

        Assert.False(failedResult.IsSuccess);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, Assert.IsType<ExecutionError>(failedResult.Error).Code);
        Assert.False(Directory.Exists(paths.ArtifactOutputDirectory.Value));
        Assert.False(File.Exists(paths.OutputManifestJsonPath.Value));
        Assert.False(Directory.Exists(ResolveStagingDirectory(paths)));

        WriteUtf8(laterSourcePath, "later output\n");
        var retriedResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.True(retriedResult.IsSuccess);
        Assert.True(Directory.Exists(paths.ArtifactOutputDirectory.Value));
        Assert.True(File.Exists(paths.OutputManifestJsonPath.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenCanceledDuringOutputCopy_DoesNotPublishOutputAndCanRetry ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "output-publication-cancellation");
        var (store, paths) = PrepareArtifacts(scope);
        WriteUnityGeneratedArtifacts(paths);
        var sourcePath = Path.Combine(paths.RunnerOutputDirectory.Value, "player.bin");
        WriteLargeFile(sourcePath);
        var request = CreateAccountingRequest(paths, sourcePath);
        using var cancellationTokenSource = new CancellationTokenSource();

        var accountingTask = store.AccountArtifactsAsync(request, cancellationTokenSource.Token).AsTask();
        await WaitForStagingAsync(paths, accountingTask);
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => accountingTask);
        Assert.False(Directory.Exists(paths.ArtifactOutputDirectory.Value));
        Assert.False(File.Exists(paths.OutputManifestJsonPath.Value));
        Assert.False(Directory.Exists(ResolveStagingDirectory(paths)));

        var retriedResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.True(retriedResult.IsSuccess);
        Assert.True(Directory.Exists(paths.ArtifactOutputDirectory.Value));
        Assert.True(File.Exists(paths.OutputManifestJsonPath.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenManifestCannotBePublished_RollsBackPublishedOutputAndCanRetry ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "manifest-publication-retry");
        var (store, paths) = PrepareArtifacts(scope);
        WriteUnityGeneratedArtifacts(paths);
        var sourcePath = Path.Combine(paths.RunnerOutputDirectory.Value, "player.bin");
        WriteUtf8(sourcePath, "player output\n");
        Directory.CreateDirectory(paths.OutputManifestJsonPath.Value);
        var request = CreateAccountingRequest(paths, sourcePath);

        var failedResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.False(failedResult.IsSuccess);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, Assert.IsType<ExecutionError>(failedResult.Error).Code);
        Assert.False(Directory.Exists(paths.ArtifactOutputDirectory.Value));
        Assert.True(Directory.Exists(paths.OutputManifestJsonPath.Value));
        Assert.False(Directory.Exists(ResolveStagingDirectory(paths)));

        Directory.Delete(paths.OutputManifestJsonPath.Value);
        var retriedResult = await store.AccountArtifactsAsync(request, CancellationToken.None);

        Assert.True(retriedResult.IsSuccess);
        Assert.True(Directory.Exists(paths.ArtifactOutputDirectory.Value));
        Assert.True(File.Exists(paths.OutputManifestJsonPath.Value));
    }

    [Theory]
    [InlineData("staging")]
    [InlineData("final")]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenReservedOutputPathIsForeignLink_RejectsItWithoutDeletingTarget (
        string reservedPathKind)
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", $"foreign-{reservedPathKind}-link");
        var (store, paths) = PrepareArtifacts(scope);
        WriteUnityGeneratedArtifacts(paths);
        var sourcePath = Path.Combine(paths.RunnerOutputDirectory.Value, "player.bin");
        WriteUtf8(sourcePath, "player output\n");
        var foreignDirectory = scope.CreateDirectory($"foreign-{reservedPathKind}");
        var foreignMarkerPath = Path.Combine(foreignDirectory, "foreign.txt");
        WriteUtf8(foreignMarkerPath, "foreign contents\n");
        var reservedPath = reservedPathKind == "staging"
            ? ResolveStagingDirectory(paths)
            : paths.ArtifactOutputDirectory.Value;
        if (!TryCreateDirectorySymbolicLink(reservedPath, foreignDirectory))
        {
            return;
        }

        var result = await store.AccountArtifactsAsync(
            CreateAccountingRequest(paths, sourcePath),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, Assert.IsType<ExecutionError>(result.Error).Code);
        Assert.True(Directory.Exists(reservedPath));
        Assert.Equal("foreign contents\n", await File.ReadAllTextAsync(foreignMarkerPath));
        Assert.False(File.Exists(paths.OutputManifestJsonPath.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AccountArtifactsAsync_WhenSameRunIsAccountedConcurrently_CommitsExactlyOneCompleteOutputTree ()
    {
        using var scope = TestDirectories.CreateTempScope("build-artifact-store", "concurrent-output-publication");
        var (store, paths) = PrepareArtifacts(scope);
        WriteUnityGeneratedArtifacts(paths);
        var firstSourcePath = Path.Combine(paths.RunnerOutputDirectory.Value, "first.bin");
        var secondSourcePath = Path.Combine(paths.RunnerOutputDirectory.Value, "second.bin");
        WriteLargeFile(firstSourcePath);
        WriteLargeFile(secondSourcePath);
        var firstTask = store
            .AccountArtifactsAsync(CreateAccountingRequest(paths, firstSourcePath), CancellationToken.None)
            .AsTask();
        var secondTask = store
            .AccountArtifactsAsync(CreateAccountingRequest(paths, secondSourcePath), CancellationToken.None)
            .AsTask();

        var results = await Task.WhenAll(firstTask, secondTask);

        var success = Assert.Single(results, static result => result.IsSuccess);
        var failure = Assert.Single(results, static result => !result.IsSuccess);
        Assert.Equal(BuildErrorCodes.BuildOutputManifestFailed, Assert.IsType<ExecutionError>(failure.Error).Code);
        var accounting = Assert.IsType<BuildRunArtifactAccountingResult>(success.Result);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(paths.OutputManifestJsonPath.Value));
        var manifestFile = Assert.Single(manifest.RootElement.GetProperty("files").EnumerateArray());
        var artifactRelativePath = manifestFile.GetProperty("artifactPath").GetString()!;
        var artifactPath = Path.Combine(
            paths.ArtifactsDirectory.Value,
            artifactRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var publishedFiles = Directory.GetFiles(paths.ArtifactOutputDirectory.Value, "*", SearchOption.AllDirectories);
        Assert.Equal([artifactPath], publishedFiles);
        Assert.Equal(accounting.OutputManifest.ManifestDigest.ToString(), manifest.RootElement.GetProperty("manifestDigest").GetString());
        Assert.Equal(
            Sha256Digest.Parse(manifestFile.GetProperty("sha256").GetString()!),
            Sha256Digest.Compute(await File.ReadAllBytesAsync(artifactPath)));
        Assert.False(Directory.Exists(ResolveStagingDirectory(paths)));
    }

    private static async Task WaitForStagingAsync (
        BuildRunArtifactPaths paths,
        Task accountingTask)
    {
        var stagingDirectory = ResolveStagingDirectory(paths);
        for (var attempt = 0; attempt < 500; attempt++)
        {
            if (Directory.Exists(stagingDirectory))
            {
                return;
            }

            if (accountingTask.IsCompleted)
            {
                break;
            }

            await Task.Delay(10);
        }

        Assert.Fail("Build output staging did not become observable before accounting completed.");
    }

    private static string ResolveStagingDirectory (BuildRunArtifactPaths paths)
    {
        return Path.Combine(paths.ArtifactsDirectory.Value, ".output-staging");
    }

    private static void WriteLargeFile (string path)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        stream.SetLength(LongRunningOutputBytes);
    }
}
