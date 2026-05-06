using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
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
        var catalogResult = await reader.ReadOpsCatalog(scope.FullPath, "fingerprint", CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifest(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.True(catalogResult.IsSuccess);
        Assert.True(manifestResult.IsSuccess);
        Assert.Equal(generatedAtUtc, catalogResult.Value!.GeneratedAtUtc);
        Assert.Equal("ops-hash", catalogResult.Value.SourceInputsHash);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, catalogResult.Value.Entries![0].Name);
        Assert.Equal(snapshot.CombinedHash, manifestResult.Value!.CombinedHash);
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
        var assetSearchResult = await reader.ReadAssetSearchLookup(scope.FullPath, "fingerprint", CancellationToken.None);
        var guidPathResult = await reader.ReadGuidPathLookup(scope.FullPath, "fingerprint", CancellationToken.None);
        var manifestResult = await reader.ReadInputsManifest(scope.FullPath, "fingerprint", CancellationToken.None);

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
        var result = await reader.ReadSceneTreeLiteLookup(
            scope.FullPath,
            "fingerprint",
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

    private static FileReadIndexArtifactWriter CreateWriter ()
    {
        return new FileReadIndexArtifactWriter(
            new IndexOpsCatalogJsonContractWriter(),
            new IndexAssetSearchLookupJsonContractWriter(),
            new IndexGuidPathLookupJsonContractWriter(),
            new IndexSceneTreeLiteLookupJsonContractWriter(),
            new IndexInputsManifestJsonContractWriter());
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
}
