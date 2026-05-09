using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexArtifactWriterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteOpsCatalog_WithManifest_WritesCatalogAndManifest ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops");
        var writer = CreateWriter();
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-08T00:00:00+00:00");
        IReadOnlyList<IndexOpEntryJsonContract> operations =
        [
            CreateGoDescribeEntry(),
        ];
        var snapshot = CreateSnapshot();

        await writer.WriteOpsCatalog(
            scope.FullPath,
            "fingerprint",
            generatedAtUtc,
            operations,
            "ops-hash",
            snapshot,
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = CreateProject(scope, "fingerprint");
        var catalogResult = await reader.ReadOpsCatalog(project, CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifest(project, CancellationToken.None);

        Assert.True(catalogResult.IsSuccess);
        Assert.True(manifestResult.IsSuccess);
        Assert.Equal(generatedAtUtc, catalogResult.Value!.GeneratedAtUtc);
        Assert.Equal("ops-hash", catalogResult.Value.SourceInputsHash);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, catalogResult.Value.Entries![0].Name);
        Assert.Equal(snapshot.CombinedHash, manifestResult.Value!.CombinedHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteOpsCatalog_WithoutManifest_WritesCatalogOnly ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-no-manifest");
        var writer = CreateWriter();
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-08T00:00:00+00:00");
        IReadOnlyList<IndexOpEntryJsonContract> operations =
        [
            CreateGoDescribeEntry(),
        ];

        await writer.WriteOpsCatalog(
            scope.FullPath,
            "fingerprint",
            generatedAtUtc,
            operations,
            "ops-hash",
            manifestInputSnapshot: null,
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = CreateProject(scope, "fingerprint");
        var catalogResult = await reader.ReadOpsCatalog(project, CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifest(project, CancellationToken.None);

        Assert.True(catalogResult.IsSuccess);
        Assert.False(manifestResult.IsSuccess);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexBootstrapFailed, manifestResult.Error!.Code);
        Assert.Equal(generatedAtUtc, catalogResult.Value!.GeneratedAtUtc);
        Assert.Equal("ops-hash", catalogResult.Value.SourceInputsHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WriteOpsCatalog_WhenCatalogWriteFails_RestoresExistingDescribeArtifact ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "ops-catalog-write-fails");
        var writer = CreateWriter();
        var reader = new FileReadIndexArtifactReader();
        var project = CreateProject(scope, "fingerprint");
        var oldOperation = CreateGoDescribeEntry();
        var newOperation = oldOperation with { Description = "Updated operation description." };

        await writer.WriteOpsCatalog(
            scope.FullPath,
            "fingerprint",
            DateTimeOffset.Parse("2026-03-08T00:00:00+00:00"),
            [oldOperation],
            "old-ops-hash",
            manifestInputSnapshot: null,
            CancellationToken.None);

        var failingWriter = CreateWriter(new ThrowingOpsCatalogJsonContractWriter());

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failingWriter.WriteOpsCatalog(
            scope.FullPath,
            "fingerprint",
            DateTimeOffset.Parse("2026-03-08T00:01:00+00:00"),
            [newOperation],
            "new-ops-hash",
            manifestInputSnapshot: null,
            CancellationToken.None));

        var catalogResult = await reader.ReadOpsCatalog(project, CancellationToken.None);
        Assert.True(catalogResult.IsSuccess);
        Assert.Equal("old-ops-hash", catalogResult.Value!.SourceInputsHash);

        var describeResult = await reader.ReadOpsDescribe(
            project,
            catalogResult.Value.Entries![0],
            catalogResult.Value.SourceInputsHash!,
            CancellationToken.None);

        Assert.True(describeResult.IsSuccess);
        Assert.Equal(oldOperation.Description, describeResult.Value!.Operation!.Description);
    }

    [Fact]
    [Trait("Size", "Small")]
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

        await writer.WriteAssetLookups(
            scope.FullPath,
            "fingerprint",
            generatedAtUtc,
            assetSearchEntries,
            guidPathEntries,
            snapshot,
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = CreateProject(scope, "fingerprint");
        var assetSearchResult = await reader.ReadAssetSearchLookup(project, CancellationToken.None);
        var guidPathResult = await reader.ReadGuidPathLookup(project, CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifest(project, CancellationToken.None);

        Assert.True(assetSearchResult.IsSuccess);
        Assert.True(guidPathResult.IsSuccess);
        Assert.True(manifestResult.IsSuccess);
        Assert.Equal(snapshot.AssetSearchHash, assetSearchResult.Value!.SourceInputsHash);
        Assert.Equal(snapshot.GuidPathHash, guidPathResult.Value!.SourceInputsHash);
        Assert.Equal("Assets/Z.asset", assetSearchResult.Value.Entries![0].AssetPath);
        Assert.Equal("Assets/Z.asset", guidPathResult.Value.Entries![0].AssetPath);
    }

    [Fact]
    [Trait("Size", "Small")]
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
                Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
        ];

        await writer.WriteSceneTreeLite(
            scope.FullPath,
            "fingerprint",
            generatedAtUtc,
            "Assets\\Scenes\\Main.unity",
            roots,
            "scene-hash",
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = CreateProject(scope, "fingerprint");
        var result = await reader.ReadSceneTreeLiteLookup(
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
    [Trait("Size", "Small")]
    public async Task WriteSceneTreeLite_WhenTargetSceneIsUpdated_PreservesOtherSceneLookup ()
    {
        using var scope = TestDirectories.CreateTempScope("read-index-writer", "scene-target");
        var writer = CreateWriter();
        var firstGeneratedAtUtc = DateTimeOffset.Parse("2026-04-14T00:00:00+00:00");
        var secondGeneratedAtUtc = DateTimeOffset.Parse("2026-04-14T00:01:00+00:00");
        var updatedGeneratedAtUtc = DateTimeOffset.Parse("2026-04-14T00:02:00+00:00");

        await writer.WriteSceneTreeLite(
            scope.FullPath,
            "fingerprint",
            firstGeneratedAtUtc,
            "Assets/Scenes/First.unity",
            [CreateSceneRoot("FirstRoot")],
            "first-hash",
            CancellationToken.None);
        await writer.WriteSceneTreeLite(
            scope.FullPath,
            "fingerprint",
            secondGeneratedAtUtc,
            "Assets/Scenes/Second.unity",
            [CreateSceneRoot("SecondRoot")],
            "second-hash",
            CancellationToken.None);
        await writer.WriteSceneTreeLite(
            scope.FullPath,
            "fingerprint",
            updatedGeneratedAtUtc,
            "Assets/Scenes/First.unity",
            [CreateSceneRoot("FirstRootUpdated")],
            "first-updated-hash",
            CancellationToken.None);

        var reader = new FileReadIndexArtifactReader();
        var project = CreateProject(scope, "fingerprint");
        var firstResult = await reader.ReadSceneTreeLiteLookup(
            project,
            "Assets/Scenes/First.unity",
            CancellationToken.None);
        var secondResult = await reader.ReadSceneTreeLiteLookup(
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
        IJsonContractWriter<IndexOpsCatalogJsonContract>? opsCatalogWriter = null)
    {
        return new FileReadIndexArtifactWriter(
            opsCatalogWriter ?? new IndexOpsCatalogJsonContractWriter(),
            new IndexOpsDescribeJsonContractWriter(),
            new IndexAssetSearchLookupJsonContractWriter(),
            new IndexGuidPathLookupJsonContractWriter(),
            new IndexSceneTreeLiteLookupJsonContractWriter(),
            new IndexInputsManifestJsonContractWriter());
    }

    private static ResolvedUnityProjectContext CreateProject (
        TestDirectoryScope scope,
        string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: scope.CreateDirectory("UnityProject"),
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static IndexOpEntryJsonContract CreateGoDescribeEntry ()
    {
        return new IndexOpEntryJsonContract(
            Name: UcliPrimitiveOperationNames.GoDescribe,
            Kind: "query",
            Policy: "safe",
            ArgsSchemaJson: """{"type":"object"}""",
            ResultSchemaJson: """{"type":"object"}""")
        {
            Description = "Returns a GameObject description including components and child hierarchy.",
            Inputs = Array.Empty<UcliOperationInputContract>(),
            ResultContract = UcliOperationResultContract.One<GameObjectDescriptionResult>("GameObject description result."),
            Assurance = new UcliOperationAssuranceContract(
                Array.Empty<string>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanModeValues.ObservesLiveUnity),
        };
    }

    private static IndexSceneTreeLiteNodeJsonContract CreateSceneRoot (string name)
    {
        return new IndexSceneTreeLiteNodeJsonContract(
            name,
            $"GlobalObjectId_V1-{name}",
            Array.Empty<IndexSceneTreeLiteNodeJsonContract>());
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

    private sealed class ThrowingOpsCatalogJsonContractWriter : IJsonContractWriter<IndexOpsCatalogJsonContract>
    {
        public string Write (IndexOpsCatalogJsonContract contract)
        {
            ArgumentNullException.ThrowIfNull(contract);
            throw new InvalidOperationException("catalog write failed.");
        }
    }
}
