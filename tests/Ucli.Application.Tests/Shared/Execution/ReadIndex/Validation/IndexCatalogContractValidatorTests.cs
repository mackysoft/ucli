using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

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
                CreateValidOpsCatalogEntry(),
            ]);

        var result = IndexCatalogContractValidator.IsValidOpsCatalog(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenDescriptionIsMissing ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                CreateValidOpsCatalogEntry() with { Description = null },
            ]);

        var result = IndexCatalogContractValidator.IsValidOpsCatalog(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenDescribeContractIsMissing ()
    {
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: new IndexOpEntryJsonContract(
                Name: "ucli.scene.open",
                Kind: UcliOperationKindValues.Command,
                Policy: OperationPolicyValues.Safe,
                ArgsSchemaJson: """{"type":"object"}"""));

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenArgsSchemaUsesUnsupportedKeyword ()
    {
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: CreateValidOpsEntry(argsSchemaJson: """{"type":"object","oneOf":[]}"""));

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenNoResultEntryHasResultSchema ()
    {
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: CreateValidOpsEntry(resultSchemaJson: """{"type":"object"}"""));

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenQueryDescribeDeclaresTouchedKinds ()
    {
        var entry = CreateValidOpsEntry() with
        {
            Kind = UcliOperationKindValues.Query,
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<string>(),
                touchedKinds: [IpcExecuteTouchedResourceKindNames.Scene],
                planMode: UcliOperationPlanModeValues.ObservesLiveUnity,
                planSemantics: "Observe scene hierarchy without applying mutation.",
                callSemantics: "Read scene hierarchy without applying mutation.",
                touchedContract: "Invalid query touched resource declaration.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation did not complete.",
                dangerousNotes: Array.Empty<string>()),
        };
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: entry);

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsDescribe_ReturnsFalse_WhenPublicOperationMayCreatePreviewState ()
    {
        var entry = CreateValidOpsEntry() with
        {
            Policy = OperationPolicyValues.Advanced,
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<string>(),
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanModeValues.MayCreatePreviewState,
                planSemantics: "Create request-local preview state before approval.",
                callSemantics: "Apply the requested operation.",
                touchedContract: "Reports no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the operation did not complete.",
                dangerousNotes: ["Preview-state planning is not public raw safe."]),
        };
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: entry);

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsTrue_WhenDescribeContractHasMultiFieldVariant ()
    {
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: CreateValidOpsEntry(
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
                                name: "sceneHierarchy",
                                description: "Use scene and hierarchy path.",
                                fields:
                                [
                                    new UcliOperationInputVariantFieldContract(
                                        name: "scene",
                                        argsPath: "$.target.scene",
                                        description: "Scene asset path.",
                                        constraints:
                                        [
                                            new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.AssetExists)
                                            {
                                                AssetKind = UcliOperationAssetKindValues.Scene,
                                            },
                                        ]),
                                    new UcliOperationInputVariantFieldContract(
                                        name: "hierarchyPath",
                                        argsPath: "$.target.hierarchyPath",
                                        description: "Hierarchy path.",
                                        constraints:
                                        [
                                            new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.HierarchyPath),
                                        ]),
                                ]),
                        ]),
                ]));

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsTrue_WhenDescribeContractHasCodeContract ()
    {
        var entry = CreateValidOpsEntry();
        entry = entry with
        {
            Policy = OperationPolicyValues.Dangerous,
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: [UcliOperationSideEffectValues.ArbitrarySourceExecution],
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanModeValues.ValidationOnly,
                planSemantics: "Validate code without applying mutation.",
                callSemantics: "Execute caller-provided source code.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Source execution may stale read surfaces.",
                failureSemantics: "Execution failure may leave indeterminate process state.",
                dangerousNotes: ["Executes caller-provided source code."]),
            CodeContract = new UcliOperationCodeContract(
                "csharp",
                new UcliCodeEntryPointContract(
                    "public static object? Run(UcliCsEvalContext context)",
                    "Compiled source must contain exactly one matching Run method.",
                    requiredStatic: true,
                    new[] { "MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext" },
                    "JSON-serializable value."),
                new[]
                {
                    new UcliCodeSourceFormContract(CsEvalSourceKindValues.CompilationUnit, "Complete C# compilation unit."),
                },
                new[]
                {
                    new UcliCodeApiTypeContract(
                        "UcliCsEvalContext",
                        "MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext",
                        "Execution context.",
                        Array.Empty<UcliCodeApiMemberContract>()),
                }),
        };
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: entry);

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenDescribeContractInputIsInvalid ()
    {
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: CreateValidOpsEntry(
                inputs:
                [
                    new UcliOperationInputContract(
                        name: "var",
                        valueType: "object",
                        description: "Object reference to resolve.",
                        constraints: Array.Empty<UcliOperationInputConstraintContract>()),
                ]));

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.False(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"type":"object","additionalProperties":false,"properties":{"var":{"type":"string"}}}""")]
    [InlineData("""{"type":"object","additionalProperties":false,"properties":{"target":{"type":"object","additionalProperties":false,"properties":{"var":{"type":"string"}}}}}""")]
    [InlineData("""{"type":"object","additionalProperties":false,"required":["var"],"properties":{"target":{"type":"string"}}}""")]
    public void IsValidOpsCatalog_ReturnsFalse_WhenArgsSchemaExposesRequestLocalAliasProperty (string argsSchemaJson)
    {
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: CreateValidOpsEntry(argsSchemaJson: argsSchemaJson));

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

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
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children:
                    [
                        new IndexSceneTreeLiteNodeJsonContract(
                            name: "Child",
                            globalObjectId: string.Empty,
                            children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                            childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                    ],
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
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
                    name: " ",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
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
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: null,
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ]);

        var result = IndexCatalogContractValidator.IsValidSceneTreeLiteLookup(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsFalse_WhenNodeStateIsMissing ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: null),
            ]);

        var result = IndexCatalogContractValidator.IsValidSceneTreeLiteLookup(contract);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidSceneTreeLiteLookup_ReturnsFalse_WhenNodeStateIsTruncatedByWindow ()
    {
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Sample.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.TruncatedByWindow),
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
                sideEffects: Array.Empty<string>(),
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanModeValues.ValidationOnly,
                planSemantics: "Validate arguments without applying mutation.",
                callSemantics: "Open an editor context without persisting project data.",
                touchedContract: "Reports no mutation resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the operation did not complete.",
                dangerousNotes: Array.Empty<string>()),
        };
    }

    private static IndexOpsCatalogEntryJsonContract CreateValidOpsCatalogEntry ()
    {
        return new IndexOpsCatalogEntryJsonContract(
            Name: "ucli.scene.open",
            Kind: UcliOperationKindValues.Command,
            Policy: OperationPolicyValues.Safe,
            Description: "Opens a Unity scene.",
            DescribeKey: new string('a', 64),
            DescribeHash: new string('b', 64));
    }
}
