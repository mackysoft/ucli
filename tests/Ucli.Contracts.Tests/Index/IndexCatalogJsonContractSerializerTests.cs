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
            CombinedHash: "combined-hash");

        var json = IndexInputsManifestJsonContractSerializer.Serialize(contract);
        var deserialized = IndexInputsManifestJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.ScriptAssembliesHash, deserialized.ScriptAssembliesHash);
        Assert.Equal(contract.PackagesManifestHash, deserialized.PackagesManifestHash);
        Assert.Equal(contract.PackagesLockHash, deserialized.PackagesLockHash);
        Assert.Equal(contract.AssemblyDefinitionHash, deserialized.AssemblyDefinitionHash);
        Assert.Equal(contract.CombinedHash, deserialized.CombinedHash);
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
                    Name: "ucli.go.describe",
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
        Assert.Equal("ucli.go.describe", deserialized.Entries[0].Name);
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
            CombinedHash: "e"));

        using var typesDocument = JsonDocument.Parse(typesCatalogJson);
        using var schemasDocument = JsonDocument.Parse(schemasCatalogJson);
        using var opsDocument = JsonDocument.Parse(opsCatalogJson);
        using var inputsDocument = JsonDocument.Parse(inputsManifestJson);

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
        Assert.True(inputsDocument.RootElement.TryGetProperty("combinedHash", out _));
    }
}