using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Indexing;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexArtifactWriterTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WithManifest_WritesCatalogAndManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops");
        var writer = CreateWriter();
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-08T00:00:00+00:00");
        var operations = CreateValidatedOperations(
            [ReadIndexOperationTestFactory.CreateGoDescribeEntry()]);
        var snapshot = CreateSnapshot();

        await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            generatedAtUtc,
            operations,
            snapshot.CombinedHash,
            snapshot,
            CancellationToken.None);

        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var catalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.True(catalogResult.IsSuccess);
        Assert.True(manifestResult.IsSuccess);
        Assert.Equal(generatedAtUtc, catalogResult.Value!.GeneratedAtUtc);
        Assert.Equal(snapshot.CombinedHash, catalogResult.Value.SourceInputsHash);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, catalogResult.Value.Entries![0].Name);
        Assert.Equal("Returns a GameObject description including components and child hierarchy.", catalogResult.Value.Entries[0].Description);
        Assert.Equal(snapshot.CombinedHash, manifestResult.Value!.Hashes.CombinedHash);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenOperationChanges_UsesContentAddressedDescribeKeyAndPreservesPreviousDetail ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-content-addressed");
        var writer = CreateWriter();
        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var firstOperation = ReadIndexOperationTestFactory.CreateGoDescribeEntry();
        var secondOperation = firstOperation with { Description = "Updated operation description." };

        await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            CreateValidatedOperations([firstOperation]),
            Sha256DigestTestFactory.Create('b'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var firstCatalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        Assert.True(firstCatalogResult.IsSuccess);
        var firstEntry = Assert.Single(firstCatalogResult.Value!.Entries!);
        var firstDescribePath = UcliStoragePathResolver.ResolveOpsDescribePath(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            firstEntry.DescribeKey!);

        await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
            CreateValidatedOperations([secondOperation]),
            Sha256DigestTestFactory.Create('c'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var secondCatalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        Assert.True(secondCatalogResult.IsSuccess);
        var secondEntry = Assert.Single(secondCatalogResult.Value!.Entries!);
        var secondDescribePath = UcliStoragePathResolver.ResolveOpsDescribePath(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            secondEntry.DescribeKey!);
        var firstDescribeResult = await reader.ReadOpsDescribeAsync(
            project,
            firstEntry,
            Sha256DigestTestFactory.Create('b'),
            CancellationToken.None);

        Assert.Equal(firstEntry.DescribeHash, firstEntry.DescribeKey);
        Assert.Equal(secondEntry.DescribeHash, secondEntry.DescribeKey);
        Assert.NotEqual(firstEntry.DescribeKey, secondEntry.DescribeKey);
        Assert.True(File.Exists(firstDescribePath.Value));
        Assert.True(File.Exists(secondDescribePath.Value));
        Assert.True(firstDescribeResult.IsSuccess);
        Assert.Equal(firstOperation.Description, firstDescribeResult.Value!.Operation.Description);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenDescribePruneRuns_PreservesDetailsReferencedByRetainedGenerations ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "retained-describe");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var writer = CreateWriter();
        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint);
        var firstOperation = ReadIndexOperationTestFactory.CreateGoDescribeEntry();
        await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:00:00Z"),
            CreateValidatedOperations([firstOperation]),
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None);
        var firstCatalog = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        var firstEntry = Assert.Single(firstCatalog.Value!.Entries);
        var firstDescribePath = UcliStoragePathResolver.ResolveOpsDescribePath(
            GetStorageRoot(scope),
            fingerprint,
            firstEntry.DescribeKey);
        File.SetLastWriteTimeUtc(firstDescribePath.Value, DateTime.UnixEpoch);
        var describeDirectory = UcliStoragePathResolver.ResolveOpsDescribeDirectory(GetStorageRoot(scope), fingerprint);
        for (var index = 0; index < 512; index++)
        {
            var orphanPath = ContainedPath.Create(
                describeDirectory,
                RootRelativePath.Parse($"orphan-{index:D4}.json")).Target;
            await File.WriteAllTextAsync(orphanPath.Value, "{}");
            File.SetLastWriteTimeUtc(
                orphanPath.Value,
                new DateTime(2090, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:01:00Z"),
            CreateValidatedOperations([firstOperation with { Description = "Updated description." }]),
            Sha256DigestTestFactory.Create('b'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        Assert.True(File.Exists(firstDescribePath.Value));
        var retainedDescribe = await reader.ReadOpsDescribeAsync(
            project,
            firstEntry,
            Sha256DigestTestFactory.Create('a'),
            CancellationToken.None);
        Assert.True(retainedDescribe.IsSuccess);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenDescribeDirectoryContainsManyOrphans_PrunesOldUnreferencedArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-describe-prune");
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var describeDirectory = UcliStoragePathResolver.ResolveOpsDescribeDirectory(
            GetStorageRoot(scope),
            projectFingerprint);
        Directory.CreateDirectory(describeDirectory.Value);
        var foreignArtifactPath = ContainedPath.Create(
            describeDirectory,
            RootRelativePath.Parse("foreign.json")).Target;
        await File.WriteAllTextAsync(foreignArtifactPath.Value, "{}");
        File.SetLastWriteTimeUtc(foreignArtifactPath.Value, DateTime.UnixEpoch);
        for (var index = 0; index < 520; index++)
        {
            await File.WriteAllTextAsync(
                CreateDescribeArtifactPath(describeDirectory, index).Value,
                "{}");
        }

        var writer = CreateWriter();
        await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            projectFingerprint,
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, projectFingerprint);
        var catalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        Assert.True(catalogResult.IsSuccess);
        var currentEntry = Assert.Single(catalogResult.Value!.Entries!);
        var currentDescribePath = UcliStoragePathResolver.ResolveOpsDescribePath(
            GetStorageRoot(scope),
            projectFingerprint,
            currentEntry.DescribeKey!);

        Assert.True(File.Exists(currentDescribePath.Value));
        Assert.True(File.Exists(foreignArtifactPath.Value));
        Assert.True(Directory
            .EnumerateFiles(describeDirectory.Value, "*.json")
            .Count(path => StoragePathSegmentCodec.IsEncodedSha256Digest(Path.GetFileNameWithoutExtension(path))) <= 513);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenOldPruneTargetIsLocked_CommitsNewCatalog ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-describe-locked-prune");
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var describeDirectory = UcliStoragePathResolver.ResolveOpsDescribeDirectory(
            GetStorageRoot(scope),
            projectFingerprint);
        Directory.CreateDirectory(describeDirectory.Value);

        var lockedArtifactPath = CreateDescribeArtifactPath(describeDirectory, 0);
        await File.WriteAllTextAsync(lockedArtifactPath.Value, "{}");
        File.SetLastWriteTimeUtc(lockedArtifactPath.Value, DateTime.UnixEpoch);
        var retainedArtifactTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var index = 1; index <= 512; index++)
        {
            var artifactPath = CreateDescribeArtifactPath(describeDirectory, index);
            await File.WriteAllTextAsync(artifactPath.Value, "{}");
            File.SetLastWriteTimeUtc(artifactPath.Value, retainedArtifactTimestamp);
        }

        using var lockedArtifact = new FileStream(
            lockedArtifactPath.Value,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-08T00:00:00+00:00");
        var writer = CreateWriter();

        await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            projectFingerprint,
            generatedAtUtc,
            CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, projectFingerprint);
        var catalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);

        Assert.True(catalogResult.IsSuccess);
        Assert.Equal(generatedAtUtc, catalogResult.Value!.GeneratedAtUtc);
        if (OperatingSystem.IsWindows())
        {
            Assert.True(File.Exists(lockedArtifactPath.Value));
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WithoutManifest_WritesCatalogOnly ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-no-manifest");
        var writer = CreateWriter();
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-08T00:00:00+00:00");
        var operations = CreateValidatedOperations(
            [ReadIndexOperationTestFactory.CreateGoDescribeEntry()]);

        await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            generatedAtUtc,
            operations,
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var catalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.True(catalogResult.IsSuccess);
        Assert.False(manifestResult.IsSuccess);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexBootstrapFailed, manifestResult.Error!.Code);
        Assert.Equal(generatedAtUtc, catalogResult.Value!.GeneratedAtUtc);
        Assert.Equal(Sha256DigestTestFactory.Create('a'), catalogResult.Value.SourceInputsHash);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenCatalogWriteFails_RestoresExistingDescribeArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-catalog-write-fails");
        var writer = CreateWriter();
        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var oldOperation = ReadIndexOperationTestFactory.CreateGoDescribeEntry();
        var newOperation = oldOperation with { Description = "Updated operation description." };

        await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            CreateValidatedOperations([oldOperation]),
            Sha256DigestTestFactory.Create('d'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var failingWriter = CreateWriter(new ThrowingJsonContractWriter<IndexOpsCatalogJsonContract>(
            new InvalidOperationException("catalog write failed.")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failingWriter.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
            CreateValidatedOperations([newOperation]),
            Sha256DigestTestFactory.Create('e'),
            manifestInputSnapshot: null,
            CancellationToken.None));

        var catalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        Assert.True(catalogResult.IsSuccess);
        Assert.Equal(Sha256DigestTestFactory.Create('d'), catalogResult.Value!.SourceInputsHash);

        var describeResult = await reader.ReadOpsDescribeAsync(
            project,
            catalogResult.Value.Entries![0],
            catalogResult.Value.SourceInputsHash!,
            CancellationToken.None);

        Assert.True(describeResult.IsSuccess);
        Assert.Equal(oldOperation.Description, describeResult.Value!.Operation.Description);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhileAnotherWriterTargetsSameFingerprint_SerializesCompleteWrites ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "same-fingerprint-serialization");
        var firstDescribeWriter = new BlockingJsonContractWriter<IndexOpsDescribeJsonContract>(
            new IndexOpsDescribeJsonContractWriter());
        var firstWriter = CreateWriter(opsDescribeWriter: firstDescribeWriter);
        var secondWriter = CreateWriter();

        var firstWriteTask = Task.Run(async () => await firstWriter.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
            CreateSnapshot().CombinedHash,
            manifestInputSnapshot: null,
            CancellationToken.None));
        await firstDescribeWriter.WaitUntilEnteredAsync();

        var secondWriteTask = secondWriter.WriteAssetLookupsAsync(
                GetStorageRoot(scope),
                ProjectFingerprintTestFactory.Create("fingerprint"),
                DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
                Array.Empty<IndexAssetSearchEntryJsonContract>(),
                Array.Empty<IndexGuidPathEntryJsonContract>(),
                CreateSnapshot(),
                CancellationToken.None)
            .AsTask();
        Assert.False(secondWriteTask.IsCompleted);

        firstDescribeWriter.Release();
        await firstWriteTask;
        await secondWriteTask;

        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var generationId = FileReadIndexArtifactReaderTestSupport.EnsureCurrentGeneration(GetStorageRoot(scope), fingerprint);
        Assert.True(File.Exists(UcliStoragePathResolver.ResolveOpsCatalogPath(GetStorageRoot(scope), fingerprint, generationId).Value));
        Assert.True(File.Exists(UcliStoragePathResolver.ResolveAssetSearchLookupPath(GetStorageRoot(scope), fingerprint, generationId).Value));
        var generation = await FileReadIndexArtifactReaderTestSupport
            .CreateReader()
            .ReadGenerationArtifactsAsync(
                ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint),
                CancellationToken.None);
        Assert.True(generation.OpsCatalog.IsSuccess);
        Assert.True(generation.AssetSearchLookup.IsSuccess);
        Assert.True(generation.GuidPathLookup.IsSuccess);
        Assert.True(generation.InputsManifest.IsSuccess);
        Assert.Equal(CreateSnapshot().CombinedHash, generation.OpsCatalog.Value!.SourceInputsHash);
        Assert.Equal(CreateSnapshot().AssetSearchHash, generation.AssetSearchLookup.Value!.SourceInputsHash);
        Assert.Equal(CreateSnapshot().GuidPathHash, generation.GuidPathLookup.Value!.SourceInputsHash);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenPointerCommitFails_KeepsPreviousGenerationVisible ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "pointer-failure");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var snapshot = CreateSnapshot();
        await CreateWriter().WriteAssetLookupsAsync(
            GetStorageRoot(scope),
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:00:00Z"),
            Array.Empty<IndexAssetSearchEntryJsonContract>(),
            Array.Empty<IndexGuidPathEntryJsonContract>(),
            snapshot,
            CancellationToken.None);
        var pointerPath = UcliStoragePathResolver.ResolveReadIndexCurrentGenerationPath(GetStorageRoot(scope), fingerprint);
        var previousPointer = await File.ReadAllTextAsync(pointerPath.Value);
        var failingPointerStore = new ThrowingPublishGenerationPointerStore(new FileReadIndexGenerationPointerStore());
        var failingWriter = CreateWriter(
            generationStore: new FileReadIndexGenerationStore(failingPointerStore, TimeProvider.System));

        await Assert.ThrowsAsync<IOException>(async () => await failingWriter.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:01:00Z"),
            CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
            snapshot.CombinedHash,
            snapshot,
            CancellationToken.None));

        Assert.Equal(previousPointer, await File.ReadAllTextAsync(pointerPath.Value));
        var generation = await FileReadIndexArtifactReaderTestSupport
            .CreateReader()
            .ReadGenerationArtifactsAsync(
                ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint),
                CancellationToken.None);
        Assert.False(generation.OpsCatalog.IsSuccess);
        Assert.True(generation.AssetSearchLookup.IsSuccess);
        Assert.True(generation.GuidPathLookup.IsSuccess);
        Assert.True(generation.InputsManifest.IsSuccess);
        Assert.Single(Directory.EnumerateDirectories(
            UcliStoragePathResolver.ResolveReadIndexGenerationsDirectory(GetStorageRoot(scope), fingerprint).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenPointerPublishesThenThrows_KeepsPublishedGenerationVisible ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "pointer-published-then-throws");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var pointerStore = new PublishThenThrowGenerationPointerStore(new FileReadIndexGenerationPointerStore());
        var writer = CreateWriter(
            generationStore: new FileReadIndexGenerationStore(pointerStore, TimeProvider.System));

        await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:00:00Z"),
            CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var catalog = await FileReadIndexArtifactReaderTestSupport
            .CreateReader()
            .ReadOpsCatalogAsync(
                ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint),
                CancellationToken.None);
        Assert.True(catalog.IsSuccess);
        Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
            GetStorageRoot(scope),
            fingerprint,
            pointerStore.PublishedGenerationId).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenPointerPublicationStateCannotBeConfirmed_RetainsPossiblyPublishedGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "pointer-publication-unknown");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var pointerStore = new UnreadableAfterPublishGenerationPointerStore(new FileReadIndexGenerationPointerStore());
        var writer = CreateWriter(
            generationStore: new FileReadIndexGenerationStore(pointerStore, TimeProvider.System));

        await Assert.ThrowsAsync<IOException>(async () => await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:00:00Z"),
            CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None));

        Assert.True(Directory.Exists(UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
            GetStorageRoot(scope),
            fingerprint,
            pointerStore.PublishedGenerationId).Value));
        var catalog = await FileReadIndexArtifactReaderTestSupport
            .CreateReader()
            .ReadOpsCatalogAsync(
                ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint),
                CancellationToken.None);
        Assert.True(catalog.IsSuccess);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenNewerOrphansExist_PrunePreservesCurrentGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "current-prune");
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var pointerStore = new AdversarialPruneGenerationPointerStore(new FileReadIndexGenerationPointerStore());
        var writer = CreateWriter(generationStore: new FileReadIndexGenerationStore(pointerStore, TimeProvider.System));

        await writer.WriteOpsCatalogAsync(
            GetStorageRoot(scope),
            fingerprint,
            DateTimeOffset.Parse("2026-07-15T00:00:00Z"),
            CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var currentDirectoryPath = UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
            GetStorageRoot(scope),
            fingerprint,
            pointerStore.PublishedGenerationId);
        Assert.True(Directory.Exists(currentDirectoryPath.Value));
        var catalog = await FileReadIndexArtifactReaderTestSupport
            .CreateReader()
            .ReadOpsCatalogAsync(
                ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, fingerprint),
                CancellationToken.None);
        Assert.True(catalog.IsSuccess);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_AfterWaitingForLock_PreservesCurrentAssetHashesInManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-manifest-rebase");
        using var heldLock = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveReadIndexWriteLockPath(GetStorageRoot(scope), ProjectFingerprintTestFactory.Create("fingerprint")),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var writer = CreateWriter();
        var suppliedSnapshot = CreateSnapshot();

        var writeTask = writer.WriteOpsCatalogAsync(
                GetStorageRoot(scope),
                ProjectFingerprintTestFactory.Create("fingerprint"),
                DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
                CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
                suppliedSnapshot.CombinedHash,
                suppliedSnapshot,
                CancellationToken.None)
            .AsTask();
        Assert.False(writeTask.IsCompleted);

        var currentManifest = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            ScriptAssembliesHash: new string('0', 64),
            PackagesManifestHash: new string('1', 64),
            PackagesLockHash: new string('2', 64),
            AssemblyDefinitionHash: new string('3', 64),
            AssetsContentHash: new string('4', 64),
            AssetSearchHash: new string('5', 64),
            GuidPathHash: new string('6', 64),
            CombinedHash: new string('7', 64));
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var generationId = FileReadIndexArtifactReaderTestSupport.EnsureCurrentGeneration(GetStorageRoot(scope), fingerprint);
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(GetStorageRoot(scope), fingerprint, generationId);
        await FileUtilities.WriteAllTextAtomicallyAsync(
            manifestPath,
            new IndexInputsManifestJsonContractWriter().Write(currentManifest),
            CancellationToken.None);

        heldLock.Dispose();
        await writeTask;

        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var manifestResult = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.True(manifestResult.IsSuccess);
        Assert.Equal(suppliedSnapshot.ScriptAssembliesHash, manifestResult.Value!.Hashes.ScriptAssembliesHash);
        Assert.Equal(suppliedSnapshot.CombinedHash, manifestResult.Value.Hashes.CombinedHash);
        Assert.Equal(Sha256DigestTestFactory.Create('4'), manifestResult.Value.Hashes.AssetsContentHash);
        Assert.Equal(Sha256DigestTestFactory.Create('5'), manifestResult.Value.Hashes.AssetSearchHash);
        Assert.Equal(Sha256DigestTestFactory.Create('6'), manifestResult.Value.Hashes.GuidPathHash);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteAssetLookups_AfterWaitingForLock_PreservesCurrentCoreHashesInManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "asset-manifest-rebase");
        using var heldLock = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveReadIndexWriteLockPath(GetStorageRoot(scope), ProjectFingerprintTestFactory.Create("fingerprint")),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var writer = CreateWriter();
        var suppliedSnapshot = CreateSnapshot();

        var writeTask = writer.WriteAssetLookupsAsync(
                GetStorageRoot(scope),
                ProjectFingerprintTestFactory.Create("fingerprint"),
                DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
                Array.Empty<IndexAssetSearchEntryJsonContract>(),
                Array.Empty<IndexGuidPathEntryJsonContract>(),
                suppliedSnapshot,
                CancellationToken.None)
            .AsTask();
        Assert.False(writeTask.IsCompleted);

        var currentManifest = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            ScriptAssembliesHash: new string('0', 64),
            PackagesManifestHash: new string('1', 64),
            PackagesLockHash: new string('2', 64),
            AssemblyDefinitionHash: new string('3', 64),
            AssetsContentHash: new string('4', 64),
            AssetSearchHash: new string('5', 64),
            GuidPathHash: new string('6', 64),
            CombinedHash: new string('7', 64));
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var generationId = FileReadIndexArtifactReaderTestSupport.EnsureCurrentGeneration(GetStorageRoot(scope), fingerprint);
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(GetStorageRoot(scope), fingerprint, generationId);
        await FileUtilities.WriteAllTextAtomicallyAsync(
            manifestPath,
            new IndexInputsManifestJsonContractWriter().Write(currentManifest),
            CancellationToken.None);

        heldLock.Dispose();
        await writeTask;

        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var manifestResult = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.True(manifestResult.IsSuccess);
        Assert.Equal(Sha256DigestTestFactory.Create('0'), manifestResult.Value!.Hashes.ScriptAssembliesHash);
        Assert.Equal(Sha256DigestTestFactory.Create('1'), manifestResult.Value.Hashes.PackagesManifestHash);
        Assert.Equal(Sha256DigestTestFactory.Create('2'), manifestResult.Value.Hashes.PackagesLockHash);
        Assert.Equal(Sha256DigestTestFactory.Create('3'), manifestResult.Value.Hashes.AssemblyDefinitionHash);
        Assert.Equal(Sha256DigestTestFactory.Create('7'), manifestResult.Value.Hashes.CombinedHash);
        Assert.Equal(suppliedSnapshot.AssetsContentHash, manifestResult.Value.Hashes.AssetsContentHash);
        Assert.Equal(suppliedSnapshot.AssetSearchHash, manifestResult.Value.Hashes.AssetSearchHash);
        Assert.Equal(suppliedSnapshot.GuidPathHash, manifestResult.Value.Hashes.GuidPathHash);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteAssetLookups_WhenSecondLookupWriteFails_RestoresPreviousArtifactSet ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "asset-rollback");
        var initialWriter = CreateWriter();
        var initialSnapshot = CreateSnapshot();
        await initialWriter.WriteAssetLookupsAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            [new IndexAssetSearchEntryJsonContract("Assets/Old.asset", "11111111111111111111111111111111", "Old", "Game.Old, Assembly-CSharp", ["Game.Old, Assembly-CSharp"])],
            [new IndexGuidPathEntryJsonContract("11111111111111111111111111111111", "Assets/Old.asset")],
            initialSnapshot,
            CancellationToken.None);

        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var generationId = FileReadIndexArtifactReaderTestSupport.EnsureCurrentGeneration(GetStorageRoot(scope), fingerprint);
        var assetSearchPath = UcliStoragePathResolver.ResolveAssetSearchLookupPath(GetStorageRoot(scope), fingerprint, generationId);
        var guidPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(GetStorageRoot(scope), fingerprint, generationId);
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(GetStorageRoot(scope), fingerprint, generationId);
        var initialAssetSearchJson = await File.ReadAllTextAsync(assetSearchPath.Value);
        var initialGuidJson = await File.ReadAllTextAsync(guidPath.Value);
        var initialManifestJson = await File.ReadAllTextAsync(manifestPath.Value);
        var failingWriter = CreateWriter(
            guidPathLookupWriter: new ThrowingJsonContractWriter<IndexGuidPathLookupJsonContract>(
                new IOException("guid lookup write failed")));
        var updatedSnapshot = initialSnapshot.WithAssetHashes(
            Sha256DigestTestFactory.Create('8'),
            Sha256DigestTestFactory.Create('9'),
            Sha256DigestTestFactory.Create('a'));

        await Assert.ThrowsAsync<IOException>(async () => await failingWriter.WriteAssetLookupsAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
            [new IndexAssetSearchEntryJsonContract("Assets/New.asset", "22222222222222222222222222222222", "New", "Game.New, Assembly-CSharp", ["Game.New, Assembly-CSharp"])],
            [new IndexGuidPathEntryJsonContract("22222222222222222222222222222222", "Assets/New.asset")],
            updatedSnapshot,
            CancellationToken.None));

        Assert.Equal(initialAssetSearchJson, await File.ReadAllTextAsync(assetSearchPath.Value));
        Assert.Equal(initialGuidJson, await File.ReadAllTextAsync(guidPath.Value));
        Assert.Equal(initialManifestJson, await File.ReadAllTextAsync(manifestPath.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteAssetLookups_WritesLookupFilesAndManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "assets");
        var writer = CreateWriter();
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-08T00:00:00+00:00");
        var snapshot = CreateSnapshot();
        IReadOnlyList<IndexAssetSearchEntryJsonContract> assetSearchEntries =
        [
            new IndexAssetSearchEntryJsonContract(
                AssetPath: "Assets/Z.asset",
                AssetGuid: "22222222222222222222222222222222",
                Name: "Z",
                TypeId: "Game.Z, Assembly-CSharp",
                SearchTypeIds:
                [
                    "Game.Z, Assembly-CSharp",
                    "UnityEngine.Object, UnityEngine.CoreModule",
                ]),
        ];
        IReadOnlyList<IndexGuidPathEntryJsonContract> guidPathEntries =
        [
            new IndexGuidPathEntryJsonContract("22222222222222222222222222222222", "Assets/Z.asset"),
        ];

        await writer.WriteAssetLookupsAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            generatedAtUtc,
            assetSearchEntries,
            guidPathEntries,
            snapshot,
            CancellationToken.None);

        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var assetSearchResult = await reader.ReadAssetSearchLookupAsync(project, CancellationToken.None);
        var guidPathResult = await reader.ReadGuidPathLookupAsync(project, CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.True(assetSearchResult.IsSuccess);
        Assert.True(guidPathResult.IsSuccess);
        Assert.True(manifestResult.IsSuccess);
        Assert.Equal(snapshot.AssetSearchHash, assetSearchResult.Value!.SourceInputsHash);
        Assert.Equal(snapshot.GuidPathHash, guidPathResult.Value!.SourceInputsHash);
        Assert.Equal("Assets/Z.asset", assetSearchResult.Value.Entries[0].AssetPath.Value);
        Assert.Equal("Assets/Z.asset", guidPathResult.Value.Entries[0].AssetPath.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteSceneTreeLite_WritesLookupAtNormalizedScenePath ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "scene");
        var writer = CreateWriter();
        var generatedAtUtc = DateTimeOffset.Parse("2026-04-14T00:00:00+00:00");
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots =
        [
            new IndexSceneTreeLiteNodeJsonContract(
                "Root",
                "GlobalObjectId_V1-2-11111111111111111111111111111111-1-0",
                Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                IndexSceneTreeLiteNodeChildrenState.Complete),
        ];

        await writer.WriteSceneTreeLiteAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            generatedAtUtc,
            new SceneAssetPath("Assets\\Scenes\\Main.unity"),
            roots,
            Sha256DigestTestFactory.Create('a'),
            CancellationToken.None);

        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var result = await reader.ReadSceneTreeLiteLookupAsync(
            project,
            new SceneAssetPath("Assets/Scenes/Main.unity"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(generatedAtUtc, result.Value!.GeneratedAtUtc);
        Assert.Equal("Assets/Scenes/Main.unity", result.Value.ScenePath.Value);
        Assert.Equal(Sha256DigestTestFactory.Create('a'), result.Value.SourceInputsHash);
        Assert.Single(result.Value.Roots);
        Assert.True(File.Exists(UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            new SceneAssetPath("Assets/Scenes/Main.unity")).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteSceneTreeLite_WhenTargetSceneIsUpdated_PreservesOtherSceneLookup ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "scene-target");
        var writer = CreateWriter();
        var firstGeneratedAtUtc = DateTimeOffset.Parse("2026-04-14T00:00:00+00:00");
        var secondGeneratedAtUtc = DateTimeOffset.Parse("2026-04-14T00:01:00+00:00");
        var updatedGeneratedAtUtc = DateTimeOffset.Parse("2026-04-14T00:02:00+00:00");

        await writer.WriteSceneTreeLiteAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            firstGeneratedAtUtc,
            new SceneAssetPath("Assets/Scenes/First.unity"),
            [CreateSceneRoot("FirstRoot")],
            Sha256DigestTestFactory.Create('a'),
            CancellationToken.None);
        await writer.WriteSceneTreeLiteAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            secondGeneratedAtUtc,
            new SceneAssetPath("Assets/Scenes/Second.unity"),
            [CreateSceneRoot("SecondRoot")],
            Sha256DigestTestFactory.Create('b'),
            CancellationToken.None);
        await writer.WriteSceneTreeLiteAsync(
            GetStorageRoot(scope),
            ProjectFingerprintTestFactory.Create("fingerprint"),
            updatedGeneratedAtUtc,
            new SceneAssetPath("Assets/Scenes/First.unity"),
            [CreateSceneRoot("FirstRootUpdated")],
            Sha256DigestTestFactory.Create('c'),
            CancellationToken.None);

        var reader = FileReadIndexArtifactReaderTestSupport.CreateReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var firstResult = await reader.ReadSceneTreeLiteLookupAsync(
            project,
            new SceneAssetPath("Assets/Scenes/First.unity"),
            CancellationToken.None);
        var secondResult = await reader.ReadSceneTreeLiteLookupAsync(
            project,
            new SceneAssetPath("Assets/Scenes/Second.unity"),
            CancellationToken.None);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(Sha256DigestTestFactory.Create('c'), firstResult.Value!.SourceInputsHash);
        Assert.Equal("FirstRootUpdated", firstResult.Value.Roots[0].Name);
        Assert.Equal(updatedGeneratedAtUtc, firstResult.Value.GeneratedAtUtc);
        Assert.Equal(Sha256DigestTestFactory.Create('b'), secondResult.Value!.SourceInputsHash);
        Assert.Equal("SecondRoot", secondResult.Value.Roots[0].Name);
        Assert.Equal(secondGeneratedAtUtc, secondResult.Value.GeneratedAtUtc);
    }

    private static FileReadIndexArtifactWriter CreateWriter (
        IJsonContractWriter<IndexOpsCatalogJsonContract>? opsCatalogWriter = null,
        IJsonContractWriter<IndexOpsDescribeJsonContract>? opsDescribeWriter = null,
        IJsonContractWriter<IndexGuidPathLookupJsonContract>? guidPathLookupWriter = null,
        FileReadIndexGenerationStore? generationStore = null)
    {
        return new FileReadIndexArtifactWriter(
            opsCatalogWriter ?? new IndexOpsCatalogJsonContractWriter(),
            opsDescribeWriter ?? new IndexOpsDescribeJsonContractWriter(),
            new IndexAssetSearchLookupJsonContractWriter(),
            guidPathLookupWriter ?? new IndexGuidPathLookupJsonContractWriter(),
            new IndexSceneTreeLiteLookupJsonContractWriter(),
            new IndexInputsManifestJsonContractWriter(),
            generationStore ?? FileReadIndexArtifactReaderTestSupport.CreateGenerationStore());
    }

    private static AbsolutePath GetStorageRoot (TestDirectoryScope scope)
    {
        return AbsolutePath.Parse(scope.FullPath);
    }

    private static AbsolutePath CreateDescribeArtifactPath (
        AbsolutePath describeDirectory,
        int index)
    {
        return ContainedPath.Create(
            describeDirectory,
            RootRelativePath.Parse(
                StoragePathSegmentCodec.EncodeSha256Digest(Sha256Digest.Parse($"{index:x64}"))
                + UcliStoragePathNames.OpsDescribeFileExtension)).Target;
    }

    private static IndexSceneTreeLiteNodeJsonContract CreateSceneRoot (string name)
    {
        return new IndexSceneTreeLiteNodeJsonContract(
            name,
            "GlobalObjectId_V1-2-11111111111111111111111111111111-1-0",
            Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
            IndexSceneTreeLiteNodeChildrenState.Complete);
    }

    private static ReadIndexInputHashSnapshot CreateSnapshot ()
    {
        return new ReadIndexInputHashSnapshot(
            Sha256DigestTestFactory.Create('0'),
            Sha256DigestTestFactory.Create('1'),
            Sha256DigestTestFactory.Create('2'),
            Sha256DigestTestFactory.Create('3'),
            Sha256DigestTestFactory.Create('4'),
            Sha256DigestTestFactory.Create('5'),
            Sha256DigestTestFactory.Create('6'),
            Sha256DigestTestFactory.Create('7'));
    }

    private static IReadOnlyList<ValidatedOpsOperation> CreateValidatedOperations (
        IReadOnlyList<IndexOpEntryJsonContract> operations)
    {
        return OperationCatalogTestFixtures.CreateSnapshot(
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            operations).Operations;
    }

    private sealed class BlockingJsonContractWriter<TContract> : IJsonContractWriter<TContract>
    {
        private readonly IJsonContractWriter<TContract> innerWriter;

        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingJsonContractWriter (IJsonContractWriter<TContract> innerWriter)
        {
            this.innerWriter = innerWriter ?? throw new ArgumentNullException(nameof(innerWriter));
        }

        public string Write (TContract contract)
        {
            entered.TrySetResult();
            release.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            return innerWriter.Write(contract);
        }

        public Task WaitUntilEnteredAsync ()
        {
            return entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void Release ()
        {
            release.TrySetResult();
        }
    }

    private sealed class ThrowingPublishGenerationPointerStore : IReadIndexGenerationPointerStore
    {
        private readonly IReadIndexGenerationPointerStore innerStore;

        public ThrowingPublishGenerationPointerStore (IReadIndexGenerationPointerStore innerStore)
        {
            this.innerStore = innerStore;
        }

        public ValueTask<Guid?> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken)
        {
            return innerStore.ReadAsync(storageRoot, projectFingerprint, cancellationToken);
        }

        public ValueTask PublishAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            Guid generationId,
            CancellationToken cancellationToken)
        {
            throw new IOException("Current generation pointer publication failed.");
        }
    }

    private sealed class PublishThenThrowGenerationPointerStore : IReadIndexGenerationPointerStore
    {
        private readonly IReadIndexGenerationPointerStore innerStore;

        public PublishThenThrowGenerationPointerStore (IReadIndexGenerationPointerStore innerStore)
        {
            this.innerStore = innerStore;
        }

        public Guid PublishedGenerationId { get; private set; }

        public ValueTask<Guid?> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken)
        {
            return innerStore.ReadAsync(storageRoot, projectFingerprint, cancellationToken);
        }

        public async ValueTask PublishAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            Guid generationId,
            CancellationToken cancellationToken)
        {
            await innerStore.PublishAsync(storageRoot, projectFingerprint, generationId, cancellationToken);
            PublishedGenerationId = generationId;
            throw new IOException("Current generation pointer publication reported a late failure.");
        }
    }

    private sealed class UnreadableAfterPublishGenerationPointerStore : IReadIndexGenerationPointerStore
    {
        private readonly IReadIndexGenerationPointerStore innerStore;

        private bool publicationAttempted;

        public UnreadableAfterPublishGenerationPointerStore (IReadIndexGenerationPointerStore innerStore)
        {
            this.innerStore = innerStore;
        }

        public Guid PublishedGenerationId { get; private set; }

        public ValueTask<Guid?> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken)
        {
            if (publicationAttempted)
            {
                throw new IOException("Current generation pointer could not be reread.");
            }

            return innerStore.ReadAsync(storageRoot, projectFingerprint, cancellationToken);
        }

        public async ValueTask PublishAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            Guid generationId,
            CancellationToken cancellationToken)
        {
            await innerStore.PublishAsync(storageRoot, projectFingerprint, generationId, cancellationToken);
            PublishedGenerationId = generationId;
            publicationAttempted = true;
            throw new IOException("Current generation pointer publication reported a late failure.");
        }
    }

    private sealed class AdversarialPruneGenerationPointerStore : IReadIndexGenerationPointerStore
    {
        private readonly IReadIndexGenerationPointerStore innerStore;

        public AdversarialPruneGenerationPointerStore (IReadIndexGenerationPointerStore innerStore)
        {
            this.innerStore = innerStore;
        }

        public Guid PublishedGenerationId { get; private set; }

        public ValueTask<Guid?> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken)
        {
            return innerStore.ReadAsync(storageRoot, projectFingerprint, cancellationToken);
        }

        public async ValueTask PublishAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            Guid generationId,
            CancellationToken cancellationToken)
        {
            await innerStore.PublishAsync(storageRoot, projectFingerprint, generationId, cancellationToken);
            PublishedGenerationId = generationId;
            Directory.SetLastWriteTimeUtc(
                UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
                    storageRoot,
                    projectFingerprint,
                    generationId).Value,
                DateTime.UnixEpoch);
            for (var index = 0; index < 9; index++)
            {
                var orphanDirectory = UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
                    storageRoot,
                    projectFingerprint,
                    Guid.NewGuid());
                Directory.CreateDirectory(orphanDirectory.Value);
                Directory.SetLastWriteTimeUtc(
                    orphanDirectory.Value,
                    new DateTime(2090, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            }
        }
    }

}
