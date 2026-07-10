using MackySoft.Tests;
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
        IReadOnlyList<IndexOpEntryJsonContract> operations =
        [
            ReadIndexOperationTestFactory.CreateGoDescribeEntry(),
        ];
        var snapshot = CreateSnapshot();

        await writer.WriteOpsCatalogAsync(
            scope.FullPath,
            "fingerprint",
            generatedAtUtc,
            operations,
            "ops-hash",
            snapshot,
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var catalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.True(catalogResult.IsSuccess);
        Assert.True(manifestResult.IsSuccess);
        Assert.Equal(generatedAtUtc, catalogResult.Value!.GeneratedAtUtc);
        Assert.Equal("ops-hash", catalogResult.Value.SourceInputsHash);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, catalogResult.Value.Entries![0].Name);
        Assert.Equal("Returns a GameObject description including components and child hierarchy.", catalogResult.Value.Entries[0].Description);
        Assert.Equal(snapshot.CombinedHash, manifestResult.Value!.CombinedHash);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenOperationChanges_UsesContentAddressedDescribeKeyAndPreservesPreviousDetail ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-content-addressed");
        var writer = CreateWriter();
        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var firstOperation = ReadIndexOperationTestFactory.CreateGoDescribeEntry();
        var secondOperation = firstOperation with { Description = "Updated operation description." };

        await writer.WriteOpsCatalogAsync(
            scope.FullPath,
            "fingerprint",
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            [firstOperation],
            "first-ops-hash",
            manifestInputSnapshot: null,
            CancellationToken.None);

        var firstCatalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        Assert.True(firstCatalogResult.IsSuccess);
        var firstEntry = Assert.Single(firstCatalogResult.Value!.Entries!);
        var firstDescribePath = UcliStoragePathResolver.ResolveOpsDescribePath(
            scope.FullPath,
            "fingerprint",
            firstEntry.DescribeKey!);

        await writer.WriteOpsCatalogAsync(
            scope.FullPath,
            "fingerprint",
            DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
            [secondOperation],
            "second-ops-hash",
            manifestInputSnapshot: null,
            CancellationToken.None);

        var secondCatalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        Assert.True(secondCatalogResult.IsSuccess);
        var secondEntry = Assert.Single(secondCatalogResult.Value!.Entries!);
        var secondDescribePath = UcliStoragePathResolver.ResolveOpsDescribePath(
            scope.FullPath,
            "fingerprint",
            secondEntry.DescribeKey!);
        var firstDescribeResult = await reader.ReadOpsDescribeAsync(
            project,
            firstEntry,
            "first-ops-hash",
            CancellationToken.None);

        Assert.Equal(firstEntry.DescribeHash, firstEntry.DescribeKey);
        Assert.Equal(secondEntry.DescribeHash, secondEntry.DescribeKey);
        Assert.NotEqual(firstEntry.DescribeKey, secondEntry.DescribeKey);
        Assert.True(File.Exists(firstDescribePath));
        Assert.True(File.Exists(secondDescribePath));
        Assert.True(firstDescribeResult.IsSuccess);
        Assert.Equal(firstOperation.Description, firstDescribeResult.Value!.Operation!.Description);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenDescribeDirectoryContainsManyOrphans_PrunesOldUnreferencedArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-describe-prune");
        const string projectFingerprint = "fingerprint";
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
            [ReadIndexOperationTestFactory.CreateGoDescribeEntry()],
            "ops-hash",
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
    public async Task WriteOpsCatalog_WithoutManifest_WritesCatalogOnly ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-no-manifest");
        var writer = CreateWriter();
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-08T00:00:00+00:00");
        IReadOnlyList<IndexOpEntryJsonContract> operations =
        [
            ReadIndexOperationTestFactory.CreateGoDescribeEntry(),
        ];

        await writer.WriteOpsCatalogAsync(
            scope.FullPath,
            "fingerprint",
            generatedAtUtc,
            operations,
            "ops-hash",
            manifestInputSnapshot: null,
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var catalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.True(catalogResult.IsSuccess);
        Assert.False(manifestResult.IsSuccess);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexBootstrapFailed, manifestResult.Error!.Code);
        Assert.Equal(generatedAtUtc, catalogResult.Value!.GeneratedAtUtc);
        Assert.Equal("ops-hash", catalogResult.Value.SourceInputsHash);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_WhenCatalogWriteFails_RestoresExistingDescribeArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-catalog-write-fails");
        var writer = CreateWriter();
        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var oldOperation = ReadIndexOperationTestFactory.CreateGoDescribeEntry();
        var newOperation = oldOperation with { Description = "Updated operation description." };

        await writer.WriteOpsCatalogAsync(
            scope.FullPath,
            "fingerprint",
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            [oldOperation],
            "old-ops-hash",
            manifestInputSnapshot: null,
            CancellationToken.None);

        var failingWriter = CreateWriter(new ThrowingJsonContractWriter<IndexOpsCatalogJsonContract>(
            new InvalidOperationException("catalog write failed.")));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failingWriter.WriteOpsCatalogAsync(
            scope.FullPath,
            "fingerprint",
            DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
            [newOperation],
            "new-ops-hash",
            manifestInputSnapshot: null,
            CancellationToken.None));

        var catalogResult = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);
        Assert.True(catalogResult.IsSuccess);
        Assert.Equal("old-ops-hash", catalogResult.Value!.SourceInputsHash);

        var describeResult = await reader.ReadOpsDescribeAsync(
            project,
            catalogResult.Value.Entries![0],
            catalogResult.Value.SourceInputsHash!,
            CancellationToken.None);

        Assert.True(describeResult.IsSuccess);
        Assert.Equal(oldOperation.Description, describeResult.Value!.Operation!.Description);
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
            "fingerprint",
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            [ReadIndexOperationTestFactory.CreateGoDescribeEntry()],
            "ops-hash",
            manifestInputSnapshot: null,
            CancellationToken.None));
        await firstDescribeWriter.WaitUntilEnteredAsync();

        var secondWriteTask = secondWriter.WriteAssetLookupsAsync(
                scope.FullPath,
                "fingerprint",
                DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
                Array.Empty<IndexAssetSearchEntryJsonContract>(),
                Array.Empty<IndexGuidPathEntryJsonContract>(),
                CreateSnapshot(),
                CancellationToken.None)
            .AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Assert.False(secondWriteTask.IsCompleted);

        firstDescribeWriter.Release();
        await firstWriteTask;
        await secondWriteTask;

        Assert.True(File.Exists(UcliStoragePathResolver.ResolveOpsCatalogPath(scope.FullPath, "fingerprint")));
        Assert.True(File.Exists(UcliStoragePathResolver.ResolveAssetSearchLookupPath(scope.FullPath, "fingerprint")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteOpsCatalog_AfterWaitingForLock_PreservesCurrentAssetHashesInManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-manifest-rebase");
        using var heldLock = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveReadIndexWriteLockPath(scope.FullPath, "fingerprint"),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var writer = CreateWriter();
        var suppliedSnapshot = CreateSnapshot();

        var writeTask = writer.WriteOpsCatalogAsync(
                scope.FullPath,
                "fingerprint",
                DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
                [ReadIndexOperationTestFactory.CreateGoDescribeEntry()],
                "ops-hash",
                suppliedSnapshot,
                CancellationToken.None)
            .AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Assert.False(writeTask.IsCompleted);

        var currentManifest = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            ScriptAssembliesHash: "current-script",
            PackagesManifestHash: "current-manifest",
            PackagesLockHash: "current-lock",
            AssemblyDefinitionHash: "current-asmdef",
            AssetsContentHash: "current-assets",
            AssetSearchHash: "current-asset-search",
            GuidPathHash: "current-guid-path",
            CombinedHash: "current-combined");
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, "fingerprint");
        await FileUtilities.WriteAllTextAtomicallyAsync(
            manifestPath,
            new IndexInputsManifestJsonContractWriter().Write(currentManifest),
            CancellationToken.None);

        heldLock.Dispose();
        await writeTask;

        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var manifestResult = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.True(manifestResult.IsSuccess);
        Assert.Equal(suppliedSnapshot.ScriptAssembliesHash, manifestResult.Value!.ScriptAssembliesHash);
        Assert.Equal(suppliedSnapshot.CombinedHash, manifestResult.Value.CombinedHash);
        Assert.Equal("current-assets", manifestResult.Value.AssetsContentHash);
        Assert.Equal("current-asset-search", manifestResult.Value.AssetSearchHash);
        Assert.Equal("current-guid-path", manifestResult.Value.GuidPathHash);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteAssetLookups_AfterWaitingForLock_PreservesCurrentCoreHashesInManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "asset-manifest-rebase");
        using var heldLock = await FileExclusiveLock.AcquireAsync(
            UcliStoragePathResolver.ResolveReadIndexWriteLockPath(scope.FullPath, "fingerprint"),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var writer = CreateWriter();
        var suppliedSnapshot = CreateSnapshot();

        var writeTask = writer.WriteAssetLookupsAsync(
                scope.FullPath,
                "fingerprint",
                DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
                Array.Empty<IndexAssetSearchEntryJsonContract>(),
                Array.Empty<IndexGuidPathEntryJsonContract>(),
                suppliedSnapshot,
                CancellationToken.None)
            .AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Assert.False(writeTask.IsCompleted);

        var currentManifest = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            ScriptAssembliesHash: "current-script",
            PackagesManifestHash: "current-manifest",
            PackagesLockHash: "current-lock",
            AssemblyDefinitionHash: "current-asmdef",
            AssetsContentHash: "current-assets",
            AssetSearchHash: "current-asset-search",
            GuidPathHash: "current-guid-path",
            CombinedHash: "current-combined");
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, "fingerprint");
        await FileUtilities.WriteAllTextAtomicallyAsync(
            manifestPath,
            new IndexInputsManifestJsonContractWriter().Write(currentManifest),
            CancellationToken.None);

        heldLock.Dispose();
        await writeTask;

        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var manifestResult = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.True(manifestResult.IsSuccess);
        Assert.Equal("current-script", manifestResult.Value!.ScriptAssembliesHash);
        Assert.Equal("current-manifest", manifestResult.Value.PackagesManifestHash);
        Assert.Equal("current-lock", manifestResult.Value.PackagesLockHash);
        Assert.Equal("current-asmdef", manifestResult.Value.AssemblyDefinitionHash);
        Assert.Equal("current-combined", manifestResult.Value.CombinedHash);
        Assert.Equal(suppliedSnapshot.AssetsContentHash, manifestResult.Value.AssetsContentHash);
        Assert.Equal(suppliedSnapshot.AssetSearchHash, manifestResult.Value.AssetSearchHash);
        Assert.Equal(suppliedSnapshot.GuidPathHash, manifestResult.Value.GuidPathHash);
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
            "fingerprint",
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            [new IndexAssetSearchEntryJsonContract("Assets/Old.asset", "old-guid", "Old", "Old.Type", ["Old.Type"])],
            [new IndexGuidPathEntryJsonContract("old-guid", "Assets/Old.asset")],
            initialSnapshot,
            CancellationToken.None);

        var assetSearchPath = UcliStoragePathResolver.ResolveAssetSearchLookupPath(scope.FullPath, "fingerprint");
        var guidPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(scope.FullPath, "fingerprint");
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, "fingerprint");
        var initialAssetSearchJson = await File.ReadAllTextAsync(assetSearchPath);
        var initialGuidJson = await File.ReadAllTextAsync(guidPath);
        var initialManifestJson = await File.ReadAllTextAsync(manifestPath);
        var failingWriter = CreateWriter(
            guidPathLookupWriter: new ThrowingJsonContractWriter<IndexGuidPathLookupJsonContract>(
                new IOException("guid lookup write failed")));
        var updatedSnapshot = initialSnapshot with
        {
            AssetsContentHash = "updated-assets",
            AssetSearchHash = "updated-asset-search",
            GuidPathHash = "updated-guid-path",
        };

        await Assert.ThrowsAsync<IOException>(async () => await failingWriter.WriteAssetLookupsAsync(
            scope.FullPath,
            "fingerprint",
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
            "fingerprint",
            generatedAtUtc,
            assetSearchEntries,
            guidPathEntries,
            snapshot,
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var assetSearchResult = await reader.ReadAssetSearchLookupAsync(project, CancellationToken.None);
        var guidPathResult = await reader.ReadGuidPathLookupAsync(project, CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.True(assetSearchResult.IsSuccess);
        Assert.True(guidPathResult.IsSuccess);
        Assert.True(manifestResult.IsSuccess);
        Assert.Equal(snapshot.AssetSearchHash, assetSearchResult.Value!.SourceInputsHash);
        Assert.Equal(snapshot.GuidPathHash, guidPathResult.Value!.SourceInputsHash);
        Assert.Equal("Assets/Z.asset", assetSearchResult.Value.Entries![0].AssetPath);
        Assert.Equal("Assets/Z.asset", guidPathResult.Value.Entries![0].AssetPath);
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
                "GlobalObjectId_V1-2-3-4",
                Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                IndexSceneTreeLiteNodeChildrenStateValues.Complete),
        ];

        await writer.WriteSceneTreeLiteAsync(
            scope.FullPath,
            "fingerprint",
            generatedAtUtc,
            "Assets\\Scenes\\Main.unity",
            roots,
            "scene-hash",
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var result = await reader.ReadSceneTreeLiteLookupAsync(
            project,
            "Assets/Scenes/Main.unity",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(generatedAtUtc, result.Value!.GeneratedAtUtc);
        Assert.Equal("Assets/Scenes/Main.unity", result.Value.ScenePath);
        Assert.Equal("scene-hash", result.Value.SourceInputsHash);
        Assert.Single(result.Value.Roots!);
        Assert.True(File.Exists(UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(
            scope.FullPath,
            "fingerprint",
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
            "fingerprint",
            firstGeneratedAtUtc,
            "Assets/Scenes/First.unity",
            [CreateSceneRoot("FirstRoot")],
            "first-hash",
            CancellationToken.None);
        await writer.WriteSceneTreeLiteAsync(
            scope.FullPath,
            "fingerprint",
            secondGeneratedAtUtc,
            "Assets/Scenes/Second.unity",
            [CreateSceneRoot("SecondRoot")],
            "second-hash",
            CancellationToken.None);
        await writer.WriteSceneTreeLiteAsync(
            scope.FullPath,
            "fingerprint",
            updatedGeneratedAtUtc,
            "Assets/Scenes/First.unity",
            [CreateSceneRoot("FirstRootUpdated")],
            "first-updated-hash",
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, "fingerprint");
        var firstResult = await reader.ReadSceneTreeLiteLookupAsync(
            project,
            "Assets/Scenes/First.unity",
            CancellationToken.None);
        var secondResult = await reader.ReadSceneTreeLiteLookupAsync(
            project,
            "Assets/Scenes/Second.unity",
            CancellationToken.None);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal("first-updated-hash", firstResult.Value!.SourceInputsHash);
        Assert.Equal("FirstRootUpdated", firstResult.Value.Roots![0].Name);
        Assert.Equal(updatedGeneratedAtUtc, firstResult.Value.GeneratedAtUtc);
        Assert.Equal("second-hash", secondResult.Value!.SourceInputsHash);
        Assert.Equal("SecondRoot", secondResult.Value.Roots![0].Name);
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
            $"GlobalObjectId_V1-{name}",
            Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
            IndexSceneTreeLiteNodeChildrenStateValues.Complete);
    }

    private static ReadIndexInputHashSnapshot CreateSnapshot ()
    {
        return new ReadIndexInputHashSnapshot(
            ScriptAssembliesHash: "script",
            PackagesManifestHash: "manifest",
            PackagesLockHash: "lock",
            AssemblyDefinitionHash: "asmdef",
            AssetsContentHash: "assets",
            AssetSearchHash: "asset-search",
            GuidPathHash: "guid-path",
            CombinedHash: "combined");
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
