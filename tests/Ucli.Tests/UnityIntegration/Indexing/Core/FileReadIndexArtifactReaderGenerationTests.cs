using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexArtifactReaderGenerationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadGenerationArtifacts_WhenPointerChangesDuringRead_DoesNotMixGenerations ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-generation-reader", "consistent-generation");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var firstSnapshot = CreateSnapshot("01234567");
        var secondSnapshot = CreateSnapshot("89abcdef");
        var pointerStore = new FileReadIndexGenerationPointerStore();
        var writer = CreateWriter(new FileReadIndexGenerationStore(pointerStore, TimeProvider.System));
        await writer.WriteAssetLookupsAsync(
            storageRoot,
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:00:00Z"),
            Array.Empty<IndexAssetSearchEntryJsonContract>(),
            Array.Empty<IndexGuidPathEntryJsonContract>(),
            firstSnapshot,
            CancellationToken.None);
        await writer.WriteOpsCatalogAsync(
            storageRoot,
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:01:00Z"),
            OperationCatalogTestFixtures.CreateSnapshot(
                DateTimeOffset.Parse("2026-07-15T00:01:00Z"),
                [ReadIndexOperationTestFactory.CreateGoDescribeEntry()]).Operations,
            firstSnapshot.CombinedHash,
            firstSnapshot,
            CancellationToken.None);
        var firstGenerationId = await pointerStore.ReadAsync(
            storageRoot,
            fingerprint,
            CancellationToken.None) ?? throw new InvalidOperationException("The first generation did not commit.");
        await writer.WriteAssetLookupsAsync(
            storageRoot,
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:02:00Z"),
            Array.Empty<IndexAssetSearchEntryJsonContract>(),
            Array.Empty<IndexGuidPathEntryJsonContract>(),
            secondSnapshot,
            CancellationToken.None);
        await writer.WriteOpsCatalogAsync(
            storageRoot,
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:03:00Z"),
            OperationCatalogTestFixtures.CreateSnapshot(
                DateTimeOffset.Parse("2026-07-15T00:03:00Z"),
                [ReadIndexOperationTestFactory.CreateGoDescribeEntry()]).Operations,
            secondSnapshot.CombinedHash,
            secondSnapshot,
            CancellationToken.None);
        var secondGenerationId = await pointerStore.ReadAsync(
            storageRoot,
            fingerprint,
            CancellationToken.None) ?? throw new InvalidOperationException("The second generation did not commit.");
        var changingPointerStore = new ChangingGenerationPointerStore(
            firstGenerationId,
            secondGenerationId);
        var reader = new FileReadIndexArtifactReader(
            new FileReadIndexGenerationStore(changingPointerStore, TimeProvider.System));
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);

        var generation = await reader.ReadGenerationArtifactsAsync(project, CancellationToken.None);

        Assert.True(generation.OpsCatalog.IsSuccess);
        Assert.True(generation.AssetSearchLookup.IsSuccess);
        Assert.True(generation.GuidPathLookup.IsSuccess);
        Assert.True(generation.InputsManifest.IsSuccess);
        Assert.Equal(firstSnapshot.CombinedHash, generation.OpsCatalog.Value!.SourceInputsHash);
        Assert.Equal(firstSnapshot.AssetSearchHash, generation.AssetSearchLookup.Value!.SourceInputsHash);
        Assert.Equal(firstSnapshot.GuidPathHash, generation.GuidPathLookup.Value!.SourceInputsHash);
        Assert.Equal(firstSnapshot, generation.InputsManifest.Value!.Hashes);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadGenerationArtifacts_WhileManyGenerationsCommit_KeepsResolvedGenerationReadable ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-generation-reader", "retention-grace");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var snapshot = CreateSnapshot("01234567");
        var pointerStore = new FileReadIndexGenerationPointerStore();
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-15T00:00:00Z"));
        var writer = CreateWriter(new FileReadIndexGenerationStore(pointerStore, timeProvider));
        await writer.WriteAssetLookupsAsync(
            storageRoot,
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:00:00Z"),
            Array.Empty<IndexAssetSearchEntryJsonContract>(),
            Array.Empty<IndexGuidPathEntryJsonContract>(),
            snapshot,
            CancellationToken.None);
        await writer.WriteOpsCatalogAsync(
            storageRoot,
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:01:00Z"),
            OperationCatalogTestFixtures.CreateSnapshot(
                DateTimeOffset.Parse("2026-07-15T00:01:00Z"),
                [ReadIndexOperationTestFactory.CreateGoDescribeEntry()]).Operations,
            snapshot.CombinedHash,
            snapshot,
            CancellationToken.None);
        var blockingPointerStore = new BlockingReadGenerationPointerStore(pointerStore);
        var reader = new FileReadIndexArtifactReader(
            new FileReadIndexGenerationStore(blockingPointerStore, timeProvider));
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var readTask = reader.ReadGenerationArtifactsAsync(project, CancellationToken.None).AsTask();
        await blockingPointerStore.WaitUntilResolvedAsync();

        try
        {
            for (var index = 0; index < 10; index++)
            {
                await writer.WriteOpsCatalogAsync(
                    storageRoot,
                    fingerprint,
                    DateTimeOffset.Parse("2026-07-15T00:02:00Z").AddMinutes(index),
                    OperationCatalogTestFixtures.CreateSnapshot(
                        DateTimeOffset.Parse("2026-07-15T00:02:00Z").AddMinutes(index),
                        [ReadIndexOperationTestFactory.CreateGoDescribeEntry()]).Operations,
                    snapshot.CombinedHash,
                    snapshot,
                    CancellationToken.None);
            }

            Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
                storageRoot,
                fingerprint,
                blockingPointerStore.ResolvedGenerationId).Value));
        }
        finally
        {
            blockingPointerStore.Release();
        }

        var generation = await readTask;
        Assert.True(generation.OpsCatalog.IsSuccess);
        Assert.True(generation.AssetSearchLookup.IsSuccess);
        Assert.True(generation.GuidPathLookup.IsSuccess);
        Assert.True(generation.InputsManifest.IsSuccess);
        Assert.Equal(snapshot.CombinedHash, generation.OpsCatalog.Value!.SourceInputsHash);
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

    private static ReadIndexInputHashSnapshot CreateSnapshot (string digestCharacters)
    {
        return new ReadIndexInputHashSnapshot(
            Sha256DigestTestFactory.Create(digestCharacters[0]),
            Sha256DigestTestFactory.Create(digestCharacters[1]),
            Sha256DigestTestFactory.Create(digestCharacters[2]),
            Sha256DigestTestFactory.Create(digestCharacters[3]),
            Sha256DigestTestFactory.Create(digestCharacters[4]),
            Sha256DigestTestFactory.Create(digestCharacters[5]),
            Sha256DigestTestFactory.Create(digestCharacters[6]),
            Sha256DigestTestFactory.Create(digestCharacters[7]));
    }

    private sealed class ChangingGenerationPointerStore : IReadIndexGenerationPointerStore
    {
        private readonly Guid firstGenerationId;

        private readonly Guid subsequentGenerationId;

        private int readCount;

        public ChangingGenerationPointerStore (
            Guid firstGenerationId,
            Guid subsequentGenerationId)
        {
            this.firstGenerationId = firstGenerationId;
            this.subsequentGenerationId = subsequentGenerationId;
        }

        public ValueTask<Guid?> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken)
        {
            var generationId = Interlocked.Increment(ref readCount) == 1
                ? firstGenerationId
                : subsequentGenerationId;
            return ValueTask.FromResult<Guid?>(generationId);
        }

        public ValueTask PublishAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            Guid generationId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException("The changing pointer fixture is read-only.");
        }
    }

    private sealed class BlockingReadGenerationPointerStore : IReadIndexGenerationPointerStore
    {
        private readonly IReadIndexGenerationPointerStore innerStore;

        private readonly TaskCompletionSource resolved = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingReadGenerationPointerStore (IReadIndexGenerationPointerStore innerStore)
        {
            this.innerStore = innerStore;
        }

        public Guid ResolvedGenerationId { get; private set; }

        public async ValueTask<Guid?> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken)
        {
            var generationId = await innerStore.ReadAsync(storageRoot, projectFingerprint, cancellationToken);
            ResolvedGenerationId = generationId
                ?? throw new InvalidOperationException("A committed generation is required by this fixture.");
            resolved.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return generationId;
        }

        public ValueTask PublishAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            Guid generationId,
            CancellationToken cancellationToken)
        {
            return innerStore.PublishAsync(storageRoot, projectFingerprint, generationId, cancellationToken);
        }

        public Task WaitUntilResolvedAsync ()
        {
            return resolved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void Release ()
        {
            release.TrySetResult();
        }
    }
}
