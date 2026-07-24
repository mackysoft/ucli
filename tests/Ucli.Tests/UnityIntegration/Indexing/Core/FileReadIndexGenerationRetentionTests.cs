using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexGenerationRetentionTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Commit_AfterDeletionGraceExpires_PrunesStaleGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-generation", "retention-expired");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var pointerStore = new FileReadIndexGenerationPointerStore();
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-15T00:00:00Z"));
        var writer = CreateWriter(new FileReadIndexGenerationStore(pointerStore, timeProvider));
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var staleGenerationId = await PrepareStaleGenerationAsync(
            writer,
            pointerStore,
            storageRoot,
            fingerprint);
        var staleGenerationPath = UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
            storageRoot,
            fingerprint,
            staleGenerationId);
        Assert.True(File.Exists(UcliStoragePathResolver.ResolveReadIndexRetentionMarkerPath(
            storageRoot,
            fingerprint,
            staleGenerationId).Value));

        timeProvider.Advance(TimeSpan.FromMinutes(6));
        await WriteGenerationAsync(writer, storageRoot, fingerprint, sequence: 10);

        Assert.False(Directory.Exists(staleGenerationPath.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Commit_WhenPointerChangesImmediatelyBeforePruneDeletion_PreservesSelectedGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-generation", "retention-pointer-reread");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var pointerStore = new FileReadIndexGenerationPointerStore();
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-15T00:00:00Z"));
        var initialWriter = CreateWriter(new FileReadIndexGenerationStore(pointerStore, timeProvider));
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var staleGenerationId = await PrepareStaleGenerationAsync(
            initialWriter,
            pointerStore,
            storageRoot,
            fingerprint);
        var staleGenerationPath = UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
            storageRoot,
            fingerprint,
            staleGenerationId);
        timeProvider.Advance(TimeSpan.FromMinutes(6));
        var switchingPointerStore = new SwitchBeforeDeletionGenerationPointerStore(
            pointerStore,
            staleGenerationId);
        var writer = CreateWriter(new FileReadIndexGenerationStore(switchingPointerStore, timeProvider));

        await WriteGenerationAsync(writer, storageRoot, fingerprint, sequence: 10);

        Assert.Equal(
            staleGenerationId,
            await pointerStore.ReadAsync(storageRoot, fingerprint, CancellationToken.None));
        Assert.True(Directory.Exists(staleGenerationPath.Value));
    }

    private static async Task<Guid> PrepareStaleGenerationAsync (
        FileReadIndexArtifactWriter writer,
        IReadIndexGenerationPointerStore pointerStore,
        AbsolutePath storageRoot,
        ProjectFingerprint fingerprint)
    {
        await WriteGenerationAsync(writer, storageRoot, fingerprint, sequence: 0);
        var staleGenerationId = await pointerStore.ReadAsync(storageRoot, fingerprint, CancellationToken.None)
            ?? throw new InvalidOperationException("The first generation did not commit.");
        Directory.SetLastWriteTimeUtc(
            UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
                storageRoot,
                fingerprint,
                staleGenerationId).Value,
            DateTime.UnixEpoch);
        for (var sequence = 1; sequence < 10; sequence++)
        {
            await WriteGenerationAsync(writer, storageRoot, fingerprint, sequence);
        }

        return staleGenerationId;
    }

    private static async Task WriteGenerationAsync (
        FileReadIndexArtifactWriter writer,
        AbsolutePath storageRoot,
        ProjectFingerprint fingerprint,
        int sequence)
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-07-15T00:00:00Z").AddMinutes(sequence);
        await writer.WriteOpsCatalogAsync(
            storageRoot,
            fingerprint,
            generatedAtUtc,
            OperationCatalogTestFixtures.CreateSnapshot(
                generatedAtUtc,
                [ReadIndexOperationTestFactory.CreateGoDescribeEntry()]).Operations,
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None);
    }

    private static FileReadIndexArtifactWriter CreateWriter (FileReadIndexGenerationStore generationStore)
    {
        return new FileReadIndexArtifactWriter(
            new IndexOpsCatalogJsonContractWriter(),
            new IndexOpsDescribeJsonContractWriter(),
            new IndexAssetSearchLookupJsonContractWriter(),
            new IndexGuidPathLookupJsonContractWriter(),
            new IndexSceneTreeLiteLookupJsonContractWriter(),
            new IndexInputsManifestJsonContractWriter(),
            generationStore);
    }

    private sealed class SwitchBeforeDeletionGenerationPointerStore : IReadIndexGenerationPointerStore
    {
        private const int DeleteGuardReadNumber = 4;

        private readonly IReadIndexGenerationPointerStore innerStore;

        private readonly Guid selectedGenerationId;

        private int readCount;

        public SwitchBeforeDeletionGenerationPointerStore (
            IReadIndexGenerationPointerStore innerStore,
            Guid selectedGenerationId)
        {
            this.innerStore = innerStore;
            this.selectedGenerationId = selectedGenerationId;
        }

        public async ValueTask<Guid?> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken)
        {
            readCount++;
            if (readCount == DeleteGuardReadNumber)
            {
                await innerStore.PublishAsync(
                    storageRoot,
                    projectFingerprint,
                    selectedGenerationId,
                    CancellationToken.None);
                return selectedGenerationId;
            }

            return await innerStore.ReadAsync(storageRoot, projectFingerprint, cancellationToken);
        }

        public ValueTask PublishAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            Guid generationId,
            CancellationToken cancellationToken)
        {
            return innerStore.PublishAsync(storageRoot, projectFingerprint, generationId, cancellationToken);
        }
    }
}
