using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsTrue_WhenDescribeContractIsComplete ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                CreateValidOpsEntry(),
            ]);

        var result = IndexCatalogContractValidator.IsValidOpsCatalog(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenDescribeContractIsMissing ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexOpEntryJsonContract(
                    Name: "ucli.scene.open",
                    Kind: UcliOperationKindValues.Command,
                    Policy: OperationPolicyValues.Safe,
                    ArgsSchemaJson: """{"type":"object"}"""),
            ]);

        var result = IndexCatalogContractValidator.IsValidOpsCatalog(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenArgsSchemaUsesUnsupportedKeyword ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                CreateValidOpsEntry(argsSchemaJson: """{"type":"object","oneOf":[]}"""),
            ]);

        var result = IndexCatalogContractValidator.IsValidOpsCatalog(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenNoResultEntryHasResultSchema ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                CreateValidOpsEntry(resultSchemaJson: """{"type":"object"}"""),
            ]);

        var result = IndexCatalogContractValidator.IsValidOpsCatalog(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenVariantFieldArgsPathIsOutsideInput ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                CreateValidOpsEntry(
                    argsSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"target":{"type":"object","additionalProperties":false,"properties":{"globalObjectId":{"type":"string"}}}}}""",
                    inputs:
                    [
                        new UcliOperationInputContract(
                            name: "target",
                            valueType: "object",
                            description: "Object reference to resolve.",
                            constraints: Array.Empty<UcliOperationInputConstraintContract>(),
                            variants:
                            [
                                new UcliOperationInputVariantContract(
                                    name: "globalObjectId",
                                    description: "Use an exact Unity GlobalObjectId.",
                                    fields:
                                    [
                                        new UcliOperationInputVariantFieldContract(
                                            name: "globalObjectId",
                                            argsPath: "$.other.globalObjectId",
                                            description: "Resolved Unity GlobalObjectId.",
                                            constraints: Array.Empty<UcliOperationInputConstraintContract>()),
                                    ]),
                            ]),
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidOpsCatalog(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenVariantFieldsAreMissing ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                CreateValidOpsEntry(
                    inputs:
                    [
                        new UcliOperationInputContract(
                            name: "target",
                            valueType: "object",
                            description: "Object reference to resolve.",
                            constraints: Array.Empty<UcliOperationInputConstraintContract>(),
                            variants:
                            [
                                new UcliOperationInputVariantContract(
                                    name: "globalObjectId",
                                    description: "Use an exact Unity GlobalObjectId.",
                                    fields: null),
                            ]),
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidOpsCatalog(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenVariantFieldConstraintIsUnsupported ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                CreateValidOpsEntry(
                    inputs:
                    [
                        new UcliOperationInputContract(
                            name: "target",
                            valueType: "object",
                            description: "Object reference to resolve.",
                            constraints: Array.Empty<UcliOperationInputConstraintContract>(),
                            variants:
                            [
                                new UcliOperationInputVariantContract(
                                    name: "globalObjectId",
                                    description: "Use an exact Unity GlobalObjectId.",
                                    fields:
                                    [
                                        new UcliOperationInputVariantFieldContract(
                                            name: "globalObjectId",
                                            argsPath: "$.target.globalObjectId",
                                            description: "Resolved Unity GlobalObjectId.",
                                            constraints:
                                            [
                                                new UcliOperationInputConstraintContract("unsupported"),
                                            ]),
                                    ]),
                            ]),
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidOpsCatalog(contract);

        Assert.False(result);
    }

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
            AssetsContentHash: "assets-hash",
            AssetSearchHash: "asset-search-hash",
            GuidPathHash: "guid-path-hash",
            CombinedHash: null);

        var result = IndexCatalogContractValidator.IsValidInputsManifest(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsTrue_WhenContractIsComplete ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-hash",
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

        var result = IndexCatalogContractValidator.IsValidSceneTreeLiteLookup(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsTrue_WhenNodeNameIsWhitespace ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    Name: " ",
                    GlobalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    Children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
            ]);

        var result = IndexCatalogContractValidator.IsValidSceneTreeLiteLookup(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsFalse_WhenChildCollectionIsMissing ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    Name: "Root",
                    GlobalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    Children: null),
            ]);

        var result = IndexCatalogContractValidator.IsValidSceneTreeLiteLookup(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsTrue_WhenContractIsComplete ()
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

        var result = IndexCatalogContractValidator.IsValidAssetSearchLookup(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsFalse_WhenAssetGuidIsDuplicated ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "asset-search-hash",
            Entries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/A.asset",
                    AssetGuid: "11111111111111111111111111111111",
                    Name: "A",
                    TypeId: "A.Type, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "A.Type, Assembly-CSharp",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/B.asset",
                    AssetGuid: "11111111111111111111111111111111",
                    Name: "B",
                    TypeId: "B.Type, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "B.Type, Assembly-CSharp",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidAssetSearchLookup(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsTrue_WhenAssetGuidIsEmpty ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "asset-search-hash",
            Entries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/A.asset",
                    AssetGuid: string.Empty,
                    Name: "A",
                    TypeId: "A.Type, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "A.Type, Assembly-CSharp",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/B.asset",
                    AssetGuid: string.Empty,
                    Name: "B",
                    TypeId: "B.Type, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "B.Type, Assembly-CSharp",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidAssetSearchLookup(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidAssetSearchLookup_ReturnsFalse_WhenNameOrTypeIdIsMissing ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "asset-search-hash",
            Entries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/A.asset",
                    AssetGuid: "11111111111111111111111111111111",
                    Name: "",
                    TypeId: null,
                    SearchTypeIds:
                    [
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ]);

        var result = IndexCatalogContractValidator.IsValidAssetSearchLookup(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidGuidPathLookup_ReturnsFalse_WhenAssetPathIsDuplicated ()
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
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "22222222222222222222222222222222",
                    AssetPath: "Assets/Data/Spawner.asset"),
            ]);

        var result = IndexCatalogContractValidator.IsValidGuidPathLookup(contract);

        Assert.False(result);
    }

    private static IndexOpEntryJsonContract CreateValidOpsEntry (
        string argsSchemaJson = """{"type":"object","additionalProperties":false,"properties":{}}""",
        string? resultSchemaJson = null,
        IReadOnlyList<UcliOperationInputContract>? inputs = null)
    {
        return new IndexOpEntryJsonContract(
            Name: "ucli.scene.open",
            Kind: UcliOperationKindValues.Command,
            Policy: OperationPolicyValues.Safe,
            ArgsSchemaJson: argsSchemaJson,
            ResultSchemaJson: resultSchemaJson)
        {
            Description = "Opens a Unity scene asset in the editor.",
            Inputs = inputs ??
            [
                new UcliOperationInputContract(
                    name: "path",
                    valueType: "string",
                    description: "Project-relative path to an existing Unity scene asset.",
                    constraints: Array.Empty<UcliOperationInputConstraintContract>()),
            ],
            ResultContract = UcliOperationResultContract.NoResult("No operation-specific result is emitted."),
            Assurance = new UcliOperationAssuranceContract(
                Array.Empty<string>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanModeValues.ValidationOnly),
        };
    }
}
