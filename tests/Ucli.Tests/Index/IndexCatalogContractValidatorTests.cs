using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Index;

namespace MackySoft.Ucli.Tests.Index;

public sealed class IndexCatalogContractValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsValidTypesCatalog_ReturnsTrue_WhenContractIsComplete ()
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

        var result = IndexCatalogContractValidator.IsValidTypesCatalog(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidTypesCatalog_ReturnsFalse_WhenTypeIdIsDuplicated ()
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
                new IndexTypeEntryJsonContract(
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    DisplayName: "SpawnerAlias",
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

        var result = IndexCatalogContractValidator.IsValidTypesCatalog(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSchemasCatalog_ReturnsFalse_WhenNonArrayContainsElementTypeId ()
    {
        var contract = new IndexSchemasCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexSchemaEntryJsonContract(
                    SchemaKey: "comp:Game.Spawner, Assembly-CSharp",
                    Kind: IndexSchemaKindCodec.ToValue(IndexSchemaKind.Comp),
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    DisplayName: "Spawner",
                    Properties:
                    [
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "field",
                            PropertyType: IndexPropertyTypeCodec.ToValue(IndexPropertyType.String),
                            DeclaredTypeId: "System.String, mscorlib",
                            IsArray: false,
                            ElementTypeId: "System.Char, mscorlib",
                            IsReadOnly: false),
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidSchemasCatalog(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSchemasCatalog_ReturnsFalse_WhenSchemaKeyDoesNotMatchKindAndTypeId ()
    {
        var contract = new IndexSchemasCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexSchemaEntryJsonContract(
                    SchemaKey: "asset:Game.Spawner, Assembly-CSharp",
                    Kind: IndexSchemaKindCodec.ToValue(IndexSchemaKind.Comp),
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    DisplayName: "Spawner",
                    Properties:
                    [
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "field",
                            PropertyType: IndexPropertyTypeCodec.ToValue(IndexPropertyType.String),
                            DeclaredTypeId: "System.String, mscorlib",
                            IsArray: false,
                            ElementTypeId: null,
                            IsReadOnly: false),
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidSchemasCatalog(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSchemasCatalog_ReturnsFalse_WhenSchemaKeyIsDuplicated ()
    {
        var contract = new IndexSchemasCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexSchemaEntryJsonContract(
                    SchemaKey: "comp:Game.Spawner, Assembly-CSharp",
                    Kind: IndexSchemaKindCodec.ToValue(IndexSchemaKind.Comp),
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    DisplayName: "Spawner",
                    Properties:
                    [
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "fieldA",
                            PropertyType: IndexPropertyTypeCodec.ToValue(IndexPropertyType.String),
                            DeclaredTypeId: "System.String, mscorlib",
                            IsArray: false,
                            ElementTypeId: null,
                            IsReadOnly: false),
                    ]),
                new IndexSchemaEntryJsonContract(
                    SchemaKey: "comp:Game.Spawner, Assembly-CSharp",
                    Kind: IndexSchemaKindCodec.ToValue(IndexSchemaKind.Comp),
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    DisplayName: "SpawnerDuplicate",
                    Properties:
                    [
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "fieldB",
                            PropertyType: IndexPropertyTypeCodec.ToValue(IndexPropertyType.String),
                            DeclaredTypeId: "System.String, mscorlib",
                            IsArray: false,
                            ElementTypeId: null,
                            IsReadOnly: false),
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidSchemasCatalog(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidInputsManifest_ReturnsFalse_WhenCombinedHashIsMissing ()
    {
        var contract = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScriptAssembliesHash: "script-hash",
            PackagesManifestHash: "manifest-hash",
            PackagesLockHash: "lock-hash",
            AssemblyDefinitionHash: "asm-hash",
            CombinedHash: null);

        var result = IndexCatalogContractValidator.IsValidInputsManifest(contract);

        Assert.False(result);
    }
}