using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileIndexCatalogReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOpsCatalog_ReturnsContract_WhenCatalogExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-success");
        var reader = new FileIndexCatalogReader();
        const string fingerprint = "fingerprint";
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexOpEntryJsonContract(
                    Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object"}"""),
            ]);
        WriteText(UcliStoragePathResolver.ResolveOpsCatalogPath(scope.FullPath, fingerprint), IndexOpsCatalogJsonContractSerializer.Serialize(contract));

        var result = await reader.ReadOpsCatalog(scope.FullPath, fingerprint, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.SchemaVersion);
        Assert.NotNull(result.Value.Entries);
        Assert.Single(result.Value.Entries);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadTypesCatalog_ReturnsContract_WhenCatalogExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "types-success");
        var reader = new FileIndexCatalogReader();
        const string fingerprint = "fingerprint";
        var contract = new IndexTypesCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexTypeEntryJsonContract(
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    DisplayName: "Spawner",
                    Namespace: "Game",
                    AssemblyName: "Assembly-CSharp",
                    BaseTypeId: "UnityEngine.MonoBehaviour, UnityEngine.CoreModule",
                    Flags: new IndexTypeFlagsJsonContract(
                        IsAbstract: false,
                        IsGenericDefinition: false,
                        IsUnityObject: true,
                        IsComponent: true,
                        IsScriptableObject: false,
                        IsSerializeReferenceCandidate: false)),
            ]);
        WriteText(UcliStoragePathResolver.ResolveTypesCatalogPath(scope.FullPath, fingerprint), IndexTypesCatalogJsonContractSerializer.Serialize(contract));

        var result = await reader.ReadTypesCatalog(scope.FullPath, fingerprint, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.SchemaVersion);
        Assert.NotNull(result.Value.Entries);
        Assert.Single(result.Value.Entries);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadSchemasCatalog_ReturnsReadIndexBootstrapFailed_WhenCatalogDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "schemas-missing");
        var reader = new FileIndexCatalogReader();

        var result = await reader.ReadSchemasCatalog(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexBootstrapFailed, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadSchemasCatalog_ReturnsReadIndexFormatInvalid_WhenCatalogJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "schemas-malformed-json");
        var reader = new FileIndexCatalogReader();
        var catalogPath = UcliStoragePathResolver.ResolveSchemasCatalogPath(scope.FullPath, "fingerprint");
        WriteText(catalogPath, "{");

        var result = await reader.ReadSchemasCatalog(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAssetSearchLookup_ReturnsContract_WhenLookupExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "asset-search-success");
        var reader = new FileIndexCatalogReader();
        const string fingerprint = "fingerprint";
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "asset-search-hash",
            Entries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/Data/Spawner.asset",
                    AssetGuid: "11111111111111111111111111111111",
                    Name: "Spawner",
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "Game.Spawner, Assembly-CSharp",
                        "UnityEngine.ScriptableObject, UnityEngine.CoreModule",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ]);
        WriteText(UcliStoragePathResolver.ResolveAssetSearchLookupPath(scope.FullPath, fingerprint), IndexAssetSearchLookupJsonContractSerializer.Serialize(contract));

        var result = await reader.ReadAssetSearchLookup(scope.FullPath, fingerprint, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Entries);
        Assert.Single(result.Value.Entries);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadGuidPathLookup_ReturnsReadIndexFormatInvalid_WhenLookupJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "guid-path-malformed");
        var reader = new FileIndexCatalogReader();
        var lookupPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(scope.FullPath, "fingerprint");
        WriteText(lookupPath, "{");

        var result = await reader.ReadGuidPathLookup(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadSceneTreeLiteLookup_ReturnsContract_WhenLookupExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "scene-tree-lite-success");
        var reader = new FileIndexCatalogReader();
        const string fingerprint = "fingerprint";
        const string scenePath = "Assets/Scenes/Sample.unity";
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: scenePath,
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    Name: "Root",
                    GlobalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    Children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
            ]);
        WriteText(UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(scope.FullPath, fingerprint, scenePath), IndexSceneTreeLiteLookupJsonContractSerializer.Serialize(contract));

        var result = await reader.ReadSceneTreeLiteLookup(scope.FullPath, fingerprint, scenePath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(scenePath, result.Value.ScenePath);
        Assert.NotNull(result.Value.Roots);
        Assert.Single(result.Value.Roots);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadSceneTreeLiteLookup_ReturnsReadIndexFormatInvalid_WhenScenePathDoesNotMatchRequestedScene ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "scene-tree-lite-mismatch");
        var reader = new FileIndexCatalogReader();
        const string fingerprint = "fingerprint";
        const string requestedScenePath = "Assets/Scenes/Sample.unity";
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Other.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    Name: "Root",
                    GlobalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    Children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
            ]);
        WriteText(UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(scope.FullPath, fingerprint, requestedScenePath), IndexSceneTreeLiteLookupJsonContractSerializer.Serialize(contract));

        var result = await reader.ReadSceneTreeLiteLookup(scope.FullPath, fingerprint, requestedScenePath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadInputsManifest_ReturnsReadIndexFormatInvalid_WhenContractIsIncomplete ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "inputs-incomplete-contract");
        var reader = new FileIndexCatalogReader();
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, "fingerprint");
        WriteText(
            manifestPath,
            """
            {
              "schemaVersion": 1,
              "generatedAtUtc": "2026-03-03T00:00:00+00:00",
              "scriptAssembliesHash": "hash",
              "packagesManifestHash": null,
              "packagesLockHash": "hash",
              "assemblyDefinitionHash": "hash",
              "assetsContentHash": "hash",
              "assetSearchHash": "hash",
              "guidPathHash": "hash",
              "combinedHash": "hash"
            }
            """);

        var result = await reader.ReadInputsManifest(scope.FullPath, "fingerprint", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

    private static void WriteText (
        string path,
        string contents)
    {
        var directoryPath = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {path}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(path, contents);
    }
}