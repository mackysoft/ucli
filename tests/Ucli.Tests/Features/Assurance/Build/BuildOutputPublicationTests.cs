using MackySoft.FileSystem;
using MackySoft.Ucli.Features.Assurance.Build;
using MackySoft.Ucli.Infrastructure.Storage;
using static MackySoft.Ucli.Tests.Features.Assurance.Build.FileBuildRunArtifactStoreTestSupport;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

public sealed class BuildOutputPublicationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Dispose_WhenCommitMarkerCannotBeRemoved_PreservesPublishedOutputTree ()
    {
        using var scope = TestDirectories.CreateTempScope("build-output-publication", "commit-marker-rollback-failure");
        var (_, paths) = PrepareArtifacts(scope);
        if (!paths.ArtifactsDirectory.TryGetParent(out var runDirectory))
        {
            throw new InvalidOperationException(
                $"Build run directory could not be resolved: {paths.ArtifactsDirectory.Value}");
        }

        using var accountingLock = FileExclusiveLock.Acquire(
            ContainedPath.Create(runDirectory, RootRelativePath.Parse(".account.lock")).Target,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var publication = BuildOutputPublication.Begin(paths);
        var stagedOutputPath = Path.Combine(publication.StagingDirectory.Value, "player.bin");
        WriteUtf8(stagedOutputPath, "player output\n");
        await publication.PrepareCommitMarkerAsync("{}", CancellationToken.None);
        publication.PublishOutput();
        await publication.PublishCommitMarkerAsync(CancellationToken.None);
        File.Delete(paths.OutputManifestJsonPath.Value);
        Directory.CreateDirectory(paths.OutputManifestJsonPath.Value);

        var exception = Assert.ThrowsAny<IOException>(publication.Dispose);

        Assert.Contains("Failed to roll back", exception.Message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(paths.OutputManifestJsonPath.Value));
        Assert.True(Directory.Exists(paths.ArtifactOutputDirectory.Value));
        Assert.Equal(
            "player output\n",
            await File.ReadAllTextAsync(Path.Combine(paths.ArtifactOutputDirectory.Value, "player.bin")));
    }
}
