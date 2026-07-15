using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
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
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            generatedAtUtc,
            operations,
            Sha256DigestTestFactory.Create('a'),
            snapshot,
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var catalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.True(catalogResult.IsSuccess);
        Assert.True(manifestResult.IsSuccess);
        Assert.Equal(generatedAtUtc, catalogResult.Value!.GeneratedAtUtc);
        Assert.Equal(Sha256DigestTestFactory.Create('a'), catalogResult.Value.SourceInputsHash);
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
        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var firstOperation = ReadIndexOperationTestFactory.CreateGoDescribeEntry();
        var secondOperation = firstOperation with { Description = "Updated operation description." };

        await writer.WriteOpsCatalogAsync(
            scope.FullPath,
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
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            firstEntry.DescribeKey!);

        await writer.WriteOpsCatalogAsync(
            scope.FullPath,
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
            scope.FullPath,
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
        Assert.True(File.Exists(firstDescribePath));
        Assert.True(File.Exists(secondDescribePath));
        Assert.True(firstDescribeResult.IsSuccess);
        Assert.Equal(firstOperation.Description, firstDescribeResult.Value!.Operation.Description);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenDescribeDirectoryContainsManyOrphans_PrunesOldUnreferencedArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-describe-prune");
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var describeDirectory = UcliStoragePathResolver.ResolveOpsDescribeDirectory(
            scope.FullPath,
            projectFingerprint);
        Directory.CreateDirectory(describeDirectory);
        for (var index = 0; index < 520; index++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(describeDirectory, $"{index:x64}.json"),
                "{}");
        }

        var writer = CreateWriter();
        await writer.WriteOpsCatalogAsync(
            scope.FullPath,
            projectFingerprint,
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, projectFingerprint);
        var catalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        Assert.True(catalogResult.IsSuccess);
        var currentEntry = Assert.Single(catalogResult.Value!.Entries!);
        var currentDescribePath = UcliStoragePathResolver.ResolveOpsDescribePath(
            scope.FullPath,
            projectFingerprint,
            currentEntry.DescribeKey!);

        Assert.True(File.Exists(currentDescribePath));
        Assert.True(Directory.EnumerateFiles(describeDirectory, "*.json").Count() <= 513);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenOldPruneTargetIsLocked_CommitsNewCatalog ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-describe-locked-prune");
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint");
        var describeDirectory = UcliStoragePathResolver.ResolveOpsDescribeDirectory(
            scope.FullPath,
            projectFingerprint);
        Directory.CreateDirectory(describeDirectory);

        var lockedArtifactPath = Path.Combine(describeDirectory, $"{0:x64}.json");
        await File.WriteAllTextAsync(lockedArtifactPath, "{}");
        File.SetLastWriteTimeUtc(lockedArtifactPath, DateTime.UnixEpoch);
        var retainedArtifactTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var index = 1; index <= 512; index++)
        {
            var artifactPath = Path.Combine(describeDirectory, $"{index:x64}.json");
            await File.WriteAllTextAsync(artifactPath, "{}");
            File.SetLastWriteTimeUtc(artifactPath, retainedArtifactTimestamp);
        }

        using var lockedArtifact = new FileStream(
            lockedArtifactPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-08T00:00:00+00:00");
        var writer = CreateWriter();

        await writer.WriteOpsCatalogAsync(
            scope.FullPath,
            projectFingerprint,
            generatedAtUtc,
            CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, projectFingerprint);
        var catalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);

        Assert.True(catalogResult.IsSuccess);
        Assert.Equal(generatedAtUtc, catalogResult.Value!.GeneratedAtUtc);
        if (OperatingSystem.IsWindows())
        {
            Assert.True(File.Exists(lockedArtifactPath));
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
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            generatedAtUtc,
            operations,
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
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
        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var oldOperation = ReadIndexOperationTestFactory.CreateGoDescribeEntry();
        var newOperation = oldOperation with { Description = "Updated operation description." };

        await writer.WriteOpsCatalogAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            CreateValidatedOperations([oldOperation]),
            Sha256DigestTestFactory.Create('d'),
            manifestInputSnapshot: null,
            CancellationToken.None);

        var failingWriter = CreateWriter(new ThrowingJsonContractWriter<IndexOpsCatalogJsonContract>(
            new InvalidOperationException("catalog write failed.")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failingWriter.WriteOpsCatalogAsync(
            scope.FullPath,
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
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
            Sha256DigestTestFactory.Create('a'),
            manifestInputSnapshot: null,
            CancellationToken.None));
        await firstDescribeWriter.WaitUntilEnteredAsync();

        var secondWriteTask = secondWriter.WriteAssetLookupsAsync(
                scope.FullPath,
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

        Assert.True(File.Exists(UcliStoragePathResolver.ResolveOpsCatalogPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"))));
        Assert.True(File.Exists(UcliStoragePathResolver.ResolveAssetSearchLookupPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"))));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_AfterWaitingForLock_PreservesCurrentAssetHashesInManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-manifest-rebase");
        using var heldLock = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveReadIndexWriteLockPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint")),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var writer = CreateWriter();
        var suppliedSnapshot = CreateSnapshot();

        var writeTask = writer.WriteOpsCatalogAsync(
                scope.FullPath,
                ProjectFingerprintTestFactory.Create("fingerprint"),
                DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
                CreateValidatedOperations([ReadIndexOperationTestFactory.CreateGoDescribeEntry()]),
                Sha256DigestTestFactory.Create('a'),
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
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        await FileUtilities.WriteAllTextAtomicallyAsync(
            manifestPath,
            new IndexInputsManifestJsonContractWriter().Write(currentManifest),
            CancellationToken.None);

        heldLock.Dispose();
        await writeTask;

        var reader = new FileReadIndexArtifactReader();
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
            UcliStoragePathResolver.ResolveReadIndexWriteLockPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint")),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var writer = CreateWriter();
        var suppliedSnapshot = CreateSnapshot();

        var writeTask = writer.WriteAssetLookupsAsync(
                scope.FullPath,
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
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        await FileUtilities.WriteAllTextAtomicallyAsync(
            manifestPath,
            new IndexInputsManifestJsonContractWriter().Write(currentManifest),
            CancellationToken.None);

        heldLock.Dispose();
        await writeTask;

        var reader = new FileReadIndexArtifactReader();
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
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            [new IndexAssetSearchEntryJsonContract("Assets/Old.asset", "old-guid", "Old", "Old.Type", ["Old.Type"])],
            [new IndexGuidPathEntryJsonContract("old-guid", "Assets/Old.asset")],
            initialSnapshot,
            CancellationToken.None);

        var assetSearchPath = UcliStoragePathResolver.ResolveAssetSearchLookupPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        var guidPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        var initialAssetSearchJson = await File.ReadAllTextAsync(assetSearchPath);
        var initialGuidJson = await File.ReadAllTextAsync(guidPath);
        var initialManifestJson = await File.ReadAllTextAsync(manifestPath);
        var failingWriter = CreateWriter(
            guidPathLookupWriter: new ThrowingJsonContractWriter<IndexGuidPathLookupJsonContract>(
                new IOException("guid lookup write failed")));
        var updatedSnapshot = initialSnapshot.WithAssetHashes(
            Sha256DigestTestFactory.Create('8'),
            Sha256DigestTestFactory.Create('9'),
            Sha256DigestTestFactory.Create('a'));

        await Assert.ThrowsAsync<IOException>(async () => await failingWriter.WriteAssetLookupsAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
            [new IndexAssetSearchEntryJsonContract("Assets/New.asset", "new-guid", "New", "New.Type", ["New.Type"])],
            [new IndexGuidPathEntryJsonContract("new-guid", "Assets/New.asset")],
            updatedSnapshot,
            CancellationToken.None));

        Assert.Equal(initialAssetSearchJson, await File.ReadAllTextAsync(assetSearchPath));
        Assert.Equal(initialGuidJson, await File.ReadAllTextAsync(guidPath));
        Assert.Equal(initialManifestJson, await File.ReadAllTextAsync(manifestPath));
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
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            generatedAtUtc,
            assetSearchEntries,
            guidPathEntries,
            snapshot,
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
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
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            generatedAtUtc,
            new SceneAssetPath("Assets\\Scenes\\Main.unity"),
            roots,
            Sha256DigestTestFactory.Create('a'),
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
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
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            "Assets/Scenes/Main.unity")));
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
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            firstGeneratedAtUtc,
            new SceneAssetPath("Assets/Scenes/First.unity"),
            [CreateSceneRoot("FirstRoot")],
            Sha256DigestTestFactory.Create('a'),
            CancellationToken.None);
        await writer.WriteSceneTreeLiteAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            secondGeneratedAtUtc,
            new SceneAssetPath("Assets/Scenes/Second.unity"),
            [CreateSceneRoot("SecondRoot")],
            Sha256DigestTestFactory.Create('b'),
            CancellationToken.None);
        await writer.WriteSceneTreeLiteAsync(
            scope.FullPath,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            updatedGeneratedAtUtc,
            new SceneAssetPath("Assets/Scenes/First.unity"),
            [CreateSceneRoot("FirstRootUpdated")],
            Sha256DigestTestFactory.Create('c'),
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
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
        IJsonContractWriter<IndexGuidPathLookupJsonContract>? guidPathLookupWriter = null)
    {
        return new FileReadIndexArtifactWriter(
            opsCatalogWriter ?? new IndexOpsCatalogJsonContractWriter(),
            opsDescribeWriter ?? new IndexOpsDescribeJsonContractWriter(),
            new IndexAssetSearchLookupJsonContractWriter(),
            guidPathLookupWriter ?? new IndexGuidPathLookupJsonContractWriter(),
            new IndexSceneTreeLiteLookupJsonContractWriter(),
            new IndexInputsManifestJsonContractWriter());
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

}
