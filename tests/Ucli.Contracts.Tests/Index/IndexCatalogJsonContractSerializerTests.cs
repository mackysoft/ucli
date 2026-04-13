using System.Text.Json;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexCatalogJsonContractSerializerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IndexTypesCatalogJsonContractSerializer_RoundTripsContract ()
    {
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

        var json = IndexTypesCatalogJsonContractSerializer.Serialize(contract);
        var deserialized = IndexTypesCatalogJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.SourceInputsHash, deserialized.SourceInputsHash);
        Assert.NotNull(deserialized.Entries);
        Assert.Single(deserialized.Entries);
        Assert.Equal("Game.Spawner, Assembly-CSharp", deserialized.Entries[0].TypeId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexSchemasCatalogJsonContractSerializer_RoundTripsContract ()
    {
        var contract = new IndexSchemasCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexSchemaEntryJsonContract(
                    SchemaKey: "comp:Game.Spawner, Assembly-CSharp",
                    Kind: IndexSchemaKindValues.Comp,
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    DisplayName: "Spawner",
                    Properties:
                    [
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "spawnInterval",
                            PropertyType: IndexPropertyTypeValues.Float,
                            DeclaredTypeId: "System.Single, mscorlib",
                            IsArray: false,
                            ElementTypeId: null,
                            IsReadOnly: false),
                    ]),
            ]);

        var json = IndexSchemasCatalogJsonContractSerializer.Serialize(contract);
        var deserialized = IndexSchemasCatalogJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        var deserializedContract = deserialized!;
        Assert.Equal(contract.SchemaVersion, deserializedContract.SchemaVersion);
        Assert.Equal(contract.SourceInputsHash, deserializedContract.SourceInputsHash);
        Assert.NotNull(deserializedContract.Entries);
        var entries = deserializedContract.Entries!;
        Assert.Single(entries);
        Assert.Equal("comp:Game.Spawner, Assembly-CSharp", entries[0].SchemaKey);
        Assert.NotNull(entries[0].Properties);
        var properties = entries[0].Properties!;
        Assert.Single(properties);
        Assert.Equal(IndexPropertyTypeValues.Float, properties[0].PropertyType);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexInputsManifestJsonContractSerializer_RoundTripsContract ()
    {
        var contract = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScriptAssembliesHash: "assemblies-hash",
            PackagesManifestHash: "manifest-hash",
            PackagesLockHash: "lock-hash",
            AssemblyDefinitionHash: "asm-hash",
            AssetsContentHash: "assets-hash",
            AssetSearchHash: "asset-search-hash",
            GuidPathHash: "guid-path-hash",
            CombinedHash: "combined-hash");

        var json = IndexInputsManifestJsonContractSerializer.Serialize(contract);
        var deserialized = IndexInputsManifestJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.ScriptAssembliesHash, deserialized.ScriptAssembliesHash);
        Assert.Equal(contract.PackagesManifestHash, deserialized.PackagesManifestHash);
        Assert.Equal(contract.PackagesLockHash, deserialized.PackagesLockHash);
        Assert.Equal(contract.AssemblyDefinitionHash, deserialized.AssemblyDefinitionHash);
        Assert.Equal(contract.AssetsContentHash, deserialized.AssetsContentHash);
        Assert.Equal(contract.AssetSearchHash, deserialized.AssetSearchHash);
        Assert.Equal(contract.GuidPathHash, deserialized.GuidPathHash);
        Assert.Equal(contract.CombinedHash, deserialized.CombinedHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexAssetSearchLookupJsonContractSerializer_RoundTripsContract ()
    {
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

        var json = IndexAssetSearchLookupJsonContractSerializer.Serialize(contract);
        var deserialized = IndexAssetSearchLookupJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.SourceInputsHash, deserialized.SourceInputsHash);
        Assert.NotNull(deserialized.Entries);
        Assert.Single(deserialized.Entries);
        Assert.Equal("Assets/Data/Spawner.asset", deserialized.Entries[0].AssetPath);
        Assert.NotNull(deserialized.Entries[0].SearchTypeIds);
        Assert.Equal(3, deserialized.Entries[0].SearchTypeIds!.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexGuidPathLookupJsonContractSerializer_RoundTripsContract ()
    {
        var contract = new IndexGuidPathLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "guid-path-hash",
            Entries:
            [
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "11111111111111111111111111111111",
                    AssetPath: "Assets/Data/Spawner.asset"),
            ]);

        var json = IndexGuidPathLookupJsonContractSerializer.Serialize(contract);
        var deserialized = IndexGuidPathLookupJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.SourceInputsHash, deserialized.SourceInputsHash);
        Assert.NotNull(deserialized.Entries);
        Assert.Single(deserialized.Entries);
        Assert.Equal("Assets/Data/Spawner.asset", deserialized.Entries[0].AssetPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexSceneTreeLiteLookupJsonContractSerializer_RoundTripsContract ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-tree-lite-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    Name: "Root",
                    GlobalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    Children:
                    [
                        new IndexSceneTreeLiteNodeJsonContract(
                            Name: "Child",
                            GlobalObjectId: string.Empty,
                            Children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
                    ]),
            ]);

        var json = IndexSceneTreeLiteLookupJsonContractSerializer.Serialize(contract);
        var deserialized = IndexSceneTreeLiteLookupJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.ScenePath, deserialized.ScenePath);
        Assert.Equal(contract.SourceInputsHash, deserialized.SourceInputsHash);
        Assert.NotNull(deserialized.Roots);
        Assert.Single(deserialized.Roots);
        Assert.Equal("Root", deserialized.Roots[0].Name);
        Assert.Single(deserialized.Roots[0].Children!);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexOpsCatalogJsonContractSerializer_RoundTripsContract ()
    {
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

        var json = IndexOpsCatalogJsonContractSerializer.Serialize(contract);
        var deserialized = IndexOpsCatalogJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.SourceInputsHash, deserialized.SourceInputsHash);
        Assert.NotNull(deserialized.Entries);
        Assert.Single(deserialized.Entries);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, deserialized.Entries[0].Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexCatalogSerializers_UseCamelCaseContractFields ()
    {
        var typesCatalogJson = IndexTypesCatalogJsonContractSerializer.Serialize(new IndexTypesCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries: Array.Empty<IndexTypeEntryJsonContract>()));
        var schemasCatalogJson = IndexSchemasCatalogJsonContractSerializer.Serialize(new IndexSchemasCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries: Array.Empty<IndexSchemaEntryJsonContract>()));
        var opsCatalogJson = IndexOpsCatalogJsonContractSerializer.Serialize(new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries: Array.Empty<IndexOpEntryJsonContract>()));
        var inputsManifestJson = IndexInputsManifestJsonContractSerializer.Serialize(new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScriptAssembliesHash: "a",
            PackagesManifestHash: "b",
            PackagesLockHash: "c",
            AssemblyDefinitionHash: "d",
            AssetsContentHash: "e",
            AssetSearchHash: "f",
            GuidPathHash: "g",
            CombinedHash: "h"));
        var assetSearchLookupJson = IndexAssetSearchLookupJsonContractSerializer.Serialize(new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries: Array.Empty<IndexAssetSearchEntryJsonContract>()));
        var guidPathLookupJson = IndexGuidPathLookupJsonContractSerializer.Serialize(new IndexGuidPathLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries: Array.Empty<IndexGuidPathEntryJsonContract>()));
        var sceneTreeLiteLookupJson = IndexSceneTreeLiteLookupJsonContractSerializer.Serialize(new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "hash",
            Roots: Array.Empty<IndexSceneTreeLiteNodeJsonContract>()));

        using var typesDocument = JsonDocument.Parse(typesCatalogJson);
        using var schemasDocument = JsonDocument.Parse(schemasCatalogJson);
        using var opsDocument = JsonDocument.Parse(opsCatalogJson);
        using var inputsDocument = JsonDocument.Parse(inputsManifestJson);
        using var assetSearchDocument = JsonDocument.Parse(assetSearchLookupJson);
        using var guidPathDocument = JsonDocument.Parse(guidPathLookupJson);
        using var sceneTreeLiteDocument = JsonDocument.Parse(sceneTreeLiteLookupJson);

        Assert.True(typesDocument.RootElement.TryGetProperty("schemaVersion", out _));
        Assert.True(typesDocument.RootElement.TryGetProperty("generatedAtUtc", out _));
        Assert.True(typesDocument.RootElement.TryGetProperty("sourceInputsHash", out _));
        Assert.True(typesDocument.RootElement.TryGetProperty("entries", out _));

        Assert.True(schemasDocument.RootElement.TryGetProperty("schemaVersion", out _));
        Assert.True(schemasDocument.RootElement.TryGetProperty("generatedAtUtc", out _));
        Assert.True(schemasDocument.RootElement.TryGetProperty("sourceInputsHash", out _));
        Assert.True(schemasDocument.RootElement.TryGetProperty("entries", out _));

        Assert.True(opsDocument.RootElement.TryGetProperty("schemaVersion", out _));
        Assert.True(opsDocument.RootElement.TryGetProperty("generatedAtUtc", out _));
        Assert.True(opsDocument.RootElement.TryGetProperty("sourceInputsHash", out _));
        Assert.True(opsDocument.RootElement.TryGetProperty("entries", out _));

        Assert.True(inputsDocument.RootElement.TryGetProperty("scriptAssembliesHash", out _));
        Assert.True(inputsDocument.RootElement.TryGetProperty("packagesManifestHash", out _));
        Assert.True(inputsDocument.RootElement.TryGetProperty("packagesLockHash", out _));
        Assert.True(inputsDocument.RootElement.TryGetProperty("assemblyDefinitionHash", out _));
        Assert.True(inputsDocument.RootElement.TryGetProperty("assetsContentHash", out _));
        Assert.True(inputsDocument.RootElement.TryGetProperty("assetSearchHash", out _));
        Assert.True(inputsDocument.RootElement.TryGetProperty("guidPathHash", out _));
        Assert.True(inputsDocument.RootElement.TryGetProperty("combinedHash", out _));

        Assert.True(assetSearchDocument.RootElement.TryGetProperty("sourceInputsHash", out _));
        Assert.True(assetSearchDocument.RootElement.TryGetProperty("entries", out _));

        Assert.True(guidPathDocument.RootElement.TryGetProperty("sourceInputsHash", out _));
        Assert.True(guidPathDocument.RootElement.TryGetProperty("entries", out _));

        Assert.True(sceneTreeLiteDocument.RootElement.TryGetProperty("scenePath", out _));
        Assert.True(sceneTreeLiteDocument.RootElement.TryGetProperty("sourceInputsHash", out _));
        Assert.True(sceneTreeLiteDocument.RootElement.TryGetProperty("roots", out _));
    }
}