using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Tests.JsonTextAssert;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexJsonContractTests
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

        var json = Write(contract);
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

        var json = Write(contract);
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

        var json = Write(contract);
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

        var json = Write(contract);
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

        var json = Write(contract);
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

        var json = Write(contract);
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
        var describe = CreateGoDescribeContract();
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexOpEntryJsonContract(
                    Name: UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object"}""",
                    ResultSchemaJson: """{"type":"object"}""")
                {
                    Description = describe.Description,
                    Inputs = describe.Inputs,
                    ResultContract = describe.ResultContract,
                    Assurance = describe.Assurance,
                },
            ]);

        var json = Write(contract);
        var deserialized = IndexOpsCatalogJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.SourceInputsHash, deserialized.SourceInputsHash);
        Assert.NotNull(deserialized.Entries);
        Assert.Single(deserialized.Entries);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, deserialized.Entries[0].Name);
        Assert.Equal(describe.Description, deserialized.Entries[0].Description);
        Assert.NotNull(deserialized.Entries[0].Inputs);
        Assert.NotNull(deserialized.Entries[0].ResultContract);
        Assert.Equal("GameObjectDescriptionResult", deserialized.Entries[0].ResultContract!.ResultType);
        Assert.NotNull(deserialized.Entries[0].Assurance);

        var targetInput = deserialized.Entries[0].Inputs!.Single(input =>
            string.Equals(input.Name, "target", StringComparison.Ordinal));
        var globalObjectIdVariant = targetInput.Variants!.Single(variant =>
            string.Equals(variant.Name, "byGlobalObjectId", StringComparison.Ordinal));
        var globalObjectIdField = Assert.Single(globalObjectIdVariant.Fields!);
        Assert.Equal("globalObjectId", globalObjectIdField.Name);
        Assert.Equal("$.target.globalObjectId", globalObjectIdField.ArgsPath);
        Assert.Equal("Resolved Unity GlobalObjectId.", globalObjectIdField.Description);
        Assert.Contains(globalObjectIdField.Constraints!, constraint => constraint.Kind == UcliOperationInputConstraintKindValues.GlobalObjectId);

        using var jsonDocument = JsonDocument.Parse(json);
        var operationElement = jsonDocument.RootElement.GetProperty("entries")[0];
        var targetInputElement = operationElement.GetProperty("inputs").EnumerateArray().Single(input =>
            string.Equals(input.GetProperty("name").GetString(), "target", StringComparison.Ordinal));
        var globalObjectIdVariantElement = targetInputElement.GetProperty("variants").EnumerateArray().Single(variant =>
            string.Equals(variant.GetProperty("name").GetString(), "byGlobalObjectId", StringComparison.Ordinal));

        Assert.False(globalObjectIdVariantElement.TryGetProperty("argsPaths", out _));
        Assert.False(globalObjectIdVariantElement.TryGetProperty("constraints", out _));
        JsonAssert.For(Assert.Single(globalObjectIdVariantElement.GetProperty("fields").EnumerateArray()))
            .HasString("name", "globalObjectId")
            .HasString("argsPath", "$.target.globalObjectId")
            .HasString("description", "Resolved Unity GlobalObjectId.")
            .HasArrayLength("constraints", 1)
            .HasProperty("constraints", 0, constraint => constraint
                .HasString("kind", UcliOperationInputConstraintKindValues.GlobalObjectId));

        var sceneHierarchyVariantElement = targetInputElement.GetProperty("variants").EnumerateArray().Single(variant =>
            string.Equals(variant.GetProperty("name").GetString(), "bySceneHierarchyPath", StringComparison.Ordinal));
        var sceneFieldElement = sceneHierarchyVariantElement.GetProperty("fields").EnumerateArray().Single(candidate =>
            string.Equals(candidate.GetProperty("name").GetString(), "scene", StringComparison.Ordinal));
        var hierarchyPathFieldElement = sceneHierarchyVariantElement.GetProperty("fields").EnumerateArray().Single(candidate =>
            string.Equals(candidate.GetProperty("name").GetString(), "hierarchyPath", StringComparison.Ordinal));

        Assert.False(sceneHierarchyVariantElement.TryGetProperty("argsPaths", out _));
        Assert.False(sceneHierarchyVariantElement.TryGetProperty("constraints", out _));
        JsonAssert.For(sceneFieldElement)
            .HasString("argsPath", "$.target.scene")
            .HasString("description", "Scene asset path for a hierarchy selector.");
        var assetExistsConstraint = sceneFieldElement.GetProperty("constraints").EnumerateArray().Single(constraint =>
            string.Equals(constraint.GetProperty("kind").GetString(), UcliOperationInputConstraintKindValues.AssetExists, StringComparison.Ordinal));
        JsonAssert.For(assetExistsConstraint)
            .HasString("assetKind", UcliOperationAssetKindValues.Scene);
        JsonAssert.For(hierarchyPathFieldElement)
            .HasString("argsPath", "$.target.hierarchyPath")
            .HasString("description", "Unity hierarchy path inside the selected scene or prefab.");
        Assert.Contains(hierarchyPathFieldElement.GetProperty("constraints").EnumerateArray(), constraint =>
            string.Equals(constraint.GetProperty("kind").GetString(), UcliOperationInputConstraintKindValues.HierarchyPath, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexJsonContractWriters_UseCamelCaseContractFields ()
    {
        var typesCatalogJson = Write(new IndexTypesCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries: Array.Empty<IndexTypeEntryJsonContract>()));
        var schemasCatalogJson = Write(new IndexSchemasCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries: Array.Empty<IndexSchemaEntryJsonContract>()));
        var opsCatalogJson = Write(new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries: Array.Empty<IndexOpEntryJsonContract>()));
        var inputsManifestJson = Write(new IndexInputsManifestJsonContract(
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
        var assetSearchLookupJson = Write(new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries: Array.Empty<IndexAssetSearchEntryJsonContract>()));
        var guidPathLookupJson = Write(new IndexGuidPathLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries: Array.Empty<IndexGuidPathEntryJsonContract>()));
        var sceneTreeLiteLookupJson = Write(new IndexSceneTreeLiteLookupJsonContract(
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

    [Fact]
    [Trait("Size", "Small")]
    public void IndexTypesCatalogJsonContractWriter_WritesFixedOrderJson ()
    {
        var contract = new IndexTypesCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries:
            [
                new IndexTypeEntryJsonContract(
                    TypeId: "Z.Type, Assembly-CSharp",
                    DisplayName: "Z",
                    Namespace: null,
                    AssemblyName: "Assembly-CSharp",
                    BaseTypeId: null,
                    Flags: new IndexTypeFlagsJsonContract(
                        IsAbstract: true,
                        IsGenericDefinition: false,
                        IsUnityObject: false,
                        IsComponent: false,
                        IsScriptableObject: false,
                        IsSerializeReferenceCandidate: false)),
                new IndexTypeEntryJsonContract(
                    TypeId: "A.Type, Assembly-CSharp",
                    DisplayName: "A",
                    Namespace: "Game",
                    AssemblyName: "Assembly-CSharp",
                    BaseTypeId: "UnityEngine.Object, UnityEngine.CoreModule",
                    Flags: new IndexTypeFlagsJsonContract(
                        IsAbstract: false,
                        IsGenericDefinition: false,
                        IsUnityObject: true,
                        IsComponent: false,
                        IsScriptableObject: true,
                        IsSerializeReferenceCandidate: false)),
            ]);

        var json = Write(contract);

        AssertExactJson(
            ExpectedJson(
                """
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-03-03T00:00:00+00:00",
                  "sourceInputsHash": "hash",
                  "entries": [
                    {
                      "typeId": "A.Type, Assembly-CSharp",
                      "displayName": "A",
                      "namespace": "Game",
                      "assemblyName": "Assembly-CSharp",
                      "baseTypeId": "UnityEngine.Object, UnityEngine.CoreModule",
                      "flags": {
                        "isAbstract": false,
                        "isGenericDefinition": false,
                        "isUnityObject": true,
                        "isComponent": false,
                        "isScriptableObject": true,
                        "isSerializeReferenceCandidate": false
                      }
                    },
                    {
                      "typeId": "Z.Type, Assembly-CSharp",
                      "displayName": "Z",
                      "namespace": null,
                      "assemblyName": "Assembly-CSharp",
                      "baseTypeId": null,
                      "flags": {
                        "isAbstract": true,
                        "isGenericDefinition": false,
                        "isUnityObject": false,
                        "isComponent": false,
                        "isScriptableObject": false,
                        "isSerializeReferenceCandidate": false
                      }
                    }
                  ]
                }
                """),
            json);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexSchemasCatalogJsonContractWriter_WritesFixedOrderJson ()
    {
        var contract = new IndexSchemasCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries:
            [
                new IndexSchemaEntryJsonContract(
                    SchemaKey: "schema:z",
                    Kind: IndexSchemaKindValues.Asset,
                    TypeId: "Z.Type, Assembly-CSharp",
                    DisplayName: "Z",
                    Properties: Array.Empty<IndexSchemaPropertyEntryJsonContract>()),
                new IndexSchemaEntryJsonContract(
                    SchemaKey: "schema:a",
                    Kind: IndexSchemaKindValues.Comp,
                    TypeId: "A.Type, Assembly-CSharp",
                    DisplayName: "A",
                    Properties:
                    [
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "z",
                            PropertyType: IndexPropertyTypeValues.String,
                            DeclaredTypeId: "System.String, mscorlib",
                            IsArray: false,
                            ElementTypeId: null,
                            IsReadOnly: true),
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "a",
                            PropertyType: IndexPropertyTypeValues.Float,
                            DeclaredTypeId: "System.Single, mscorlib",
                            IsArray: true,
                            ElementTypeId: "System.Single, mscorlib",
                            IsReadOnly: false),
                    ]),
            ]);

        var json = Write(contract);

        AssertExactJson(
            ExpectedJson(
                """
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-03-03T00:00:00+00:00",
                  "sourceInputsHash": "hash",
                  "entries": [
                    {
                      "schemaKey": "schema:a",
                      "kind": "comp",
                      "typeId": "A.Type, Assembly-CSharp",
                      "displayName": "A",
                      "properties": [
                        {
                          "path": "a",
                          "propertyType": "float",
                          "declaredTypeId": "System.Single, mscorlib",
                          "isArray": true,
                          "elementTypeId": "System.Single, mscorlib",
                          "isReadOnly": false
                        },
                        {
                          "path": "z",
                          "propertyType": "string",
                          "declaredTypeId": "System.String, mscorlib",
                          "isArray": false,
                          "elementTypeId": null,
                          "isReadOnly": true
                        }
                      ]
                    },
                    {
                      "schemaKey": "schema:z",
                      "kind": "asset",
                      "typeId": "Z.Type, Assembly-CSharp",
                      "displayName": "Z",
                      "properties": []
                    }
                  ]
                }
                """),
            json);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexOpsCatalogJsonContractWriter_WritesFixedOrderJsonAndOmitPolicy ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries:
            [
                new IndexOpEntryJsonContract(
                    Name: "z.op",
                    Kind: "mutation",
                    Policy: "dangerous",
                    ArgsSchemaJson: """{"type":"object"}""",
                    ResultSchemaJson: """{"type":"object"}"""),
                new IndexOpEntryJsonContract(
                    Name: "a.op",
                    Kind: "query",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object"}""")
                {
                    Description = "A operation.",
                    Inputs =
                    [
                        new UcliOperationInputContract(
                            name: "target",
                            valueType: "object",
                            description: "Target input.",
                            constraints:
                            [
                                new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.AssetExists),
                            ]),
                    ],
                },
            ]);

        var json = Write(contract);

        AssertExactJson(
            ExpectedJson(
                """
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-03-03T00:00:00+00:00",
                  "sourceInputsHash": "hash",
                  "entries": [
                    {
                      "name": "a.op",
                      "kind": "query",
                      "policy": "safe",
                      "argsSchemaJson": "{\u0022type\u0022:\u0022object\u0022}",
                      "description": "A operation.",
                      "inputs": [
                        {
                          "name": "target",
                          "description": "Target input.",
                          "valueType": "object",
                          "constraints": [
                            {
                              "kind": "assetExists"
                            }
                          ]
                        }
                      ],
                      "resultContract": null,
                      "assurance": null
                    },
                    {
                      "name": "z.op",
                      "kind": "mutation",
                      "policy": "dangerous",
                      "argsSchemaJson": "{\u0022type\u0022:\u0022object\u0022}",
                      "resultSchemaJson": "{\u0022type\u0022:\u0022object\u0022}",
                      "description": null,
                      "inputs": null,
                      "resultContract": null,
                      "assurance": null
                    }
                  ]
                }
                """),
            json);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexOpsCatalogJsonContractWriter_WritesOptionalOperationMetadata ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries:
            [
                new IndexOpEntryJsonContract(
                    Name: "write.asset",
                    Kind: "mutation",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object"}""",
                    ResultSchemaJson: """{"$ref":"#/definitions/WriteResult"}""")
                {
                    Description = "Writes one asset.",
                    Inputs =
                    [
                        new UcliOperationInputContract(
                            name: "target",
                            valueType: "object",
                            description: "Target input.",
                            constraints:
                            [
                                new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.AssetExists)
                                {
                                    AssetKind = UcliOperationAssetKindValues.Scene,
                                },
                                new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.ReferenceResolvable)
                                {
                                    TargetKind = UcliOperationReferenceTargetKindValues.GameObject,
                                },
                                new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.TypeAssignableTo)
                                {
                                    TypeKind = UcliOperationTypeKindValues.Component,
                                },
                                new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.SerializedProperty)
                                {
                                    Access = UcliOperationSerializedPropertyAccessValues.Write,
                                },
                                new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.Range)
                                {
                                    Min = 1.5,
                                    Max = 3.5,
                                },
                            ],
                            argsPath: "$.target",
                            variants:
                            [
                                new UcliOperationInputVariantContract(
                                    name: "byPath",
                                    description: "Path selector.",
                                    fields:
                                    [
                                        new UcliOperationInputVariantFieldContract(
                                            name: "path",
                                            argsPath: "$.target.path",
                                            description: "Serialized path.",
                                            constraints:
                                            [
                                                new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.NonEmpty),
                                            ]),
                                    ]),
                            ]),
                    ],
                    ResultContract = new UcliOperationResultContract(
                        emitted: true,
                        resultType: "WriteResult",
                        description: "Written result."),
                    Assurance = new UcliOperationAssuranceContract(
                        sideEffects:
                        [
                            UcliOperationSideEffectValues.WritesAsset,
                        ],
                        mayDirty: true,
                        mayPersist: true,
                        touchedKinds:
                        [
                            IpcExecuteTouchedResourceKindNames.Asset,
                        ],
                        planMode: UcliOperationPlanModeValues.MayCreatePreviewState),
                },
            ]);

        var json = Write(contract);

        AssertExactJson(
            ExpectedJson(
                """
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-03-03T00:00:00+00:00",
                  "sourceInputsHash": "hash",
                  "entries": [
                    {
                      "name": "write.asset",
                      "kind": "mutation",
                      "policy": "safe",
                      "argsSchemaJson": "{\u0022type\u0022:\u0022object\u0022}",
                      "resultSchemaJson": "{\u0022$ref\u0022:\u0022#/definitions/WriteResult\u0022}",
                      "description": "Writes one asset.",
                      "inputs": [
                        {
                          "name": "target",
                          "description": "Target input.",
                          "valueType": "object",
                          "constraints": [
                            {
                              "kind": "assetExists",
                              "assetKind": "scene"
                            },
                            {
                              "kind": "referenceResolvable",
                              "targetKind": "gameObject"
                            },
                            {
                              "kind": "typeAssignableTo",
                              "typeKind": "component"
                            },
                            {
                              "kind": "serializedProperty",
                              "access": "write"
                            },
                            {
                              "kind": "range",
                              "min": 1.5,
                              "max": 3.5
                            }
                          ],
                          "argsPath": "$.target",
                          "variants": [
                            {
                              "name": "byPath",
                              "description": "Path selector.",
                              "fields": [
                                {
                                  "name": "path",
                                  "argsPath": "$.target.path",
                                  "description": "Serialized path.",
                                  "constraints": [
                                    {
                                      "kind": "nonEmpty"
                                    }
                                  ]
                                }
                              ]
                            }
                          ]
                        }
                      ],
                      "resultContract": {
                        "emitted": true,
                        "resultType": "WriteResult",
                        "description": "Written result."
                      },
                      "assurance": {
                        "sideEffects": [
                          "writesAsset"
                        ],
                        "mayDirty": true,
                        "mayPersist": true,
                        "touchedKinds": [
                          "asset"
                        ],
                        "planMode": "mayCreatePreviewState"
                      }
                    }
                  ]
                }
                """),
            json);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IndexLookupAndManifestJsonContractWriters_WriteFixedOrderJson ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-03T00:00:00+00:00");
        var assetSearchLookup = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: "asset-hash",
            Entries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/Z.asset",
                    AssetGuid: "z-guid",
                    Name: "Z",
                    TypeId: "Z.Type, Assembly-CSharp",
                    SearchTypeIds: ["Z.Type, Assembly-CSharp"]),
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/A.asset",
                    AssetGuid: "a-guid",
                    Name: "A",
                    TypeId: "A.Type, Assembly-CSharp",
                    SearchTypeIds: ["A.Type, Assembly-CSharp"]),
            ]);
        var guidPathLookup = new IndexGuidPathLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: generatedAtUtc,
            SourceInputsHash: "guid-hash",
            Entries:
            [
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "z-guid",
                    AssetPath: "Assets/Z.asset"),
                new IndexGuidPathEntryJsonContract(
                    AssetGuid: "a-guid",
                    AssetPath: "Assets/A.asset"),
            ]);
        var sceneTreeLiteLookup = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: generatedAtUtc,
            ScenePath: "Assets/Scenes/Main.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    Name: "RootZ",
                    GlobalObjectId: "z",
                    Children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
                new IndexSceneTreeLiteNodeJsonContract(
                    Name: "RootA",
                    GlobalObjectId: "a",
                    Children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>()),
            ]);
        var inputsManifest = new IndexInputsManifestJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: generatedAtUtc,
            ScriptAssembliesHash: "script",
            PackagesManifestHash: "manifest",
            PackagesLockHash: "lock",
            AssemblyDefinitionHash: "asmdef",
            AssetsContentHash: "assets",
            AssetSearchHash: "asset",
            GuidPathHash: "guid",
            CombinedHash: "combined");

        AssertExactJson(
            ExpectedJson(
                """
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-03-03T00:00:00+00:00",
                  "sourceInputsHash": "asset-hash",
                  "entries": [
                    {
                      "assetPath": "Assets/A.asset",
                      "assetGuid": "a-guid",
                      "name": "A",
                      "typeId": "A.Type, Assembly-CSharp",
                      "searchTypeIds": [
                        "A.Type, Assembly-CSharp"
                      ]
                    },
                    {
                      "assetPath": "Assets/Z.asset",
                      "assetGuid": "z-guid",
                      "name": "Z",
                      "typeId": "Z.Type, Assembly-CSharp",
                      "searchTypeIds": [
                        "Z.Type, Assembly-CSharp"
                      ]
                    }
                  ]
                }
                """),
            Write(assetSearchLookup));
        AssertExactJson(
            ExpectedJson(
                """
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-03-03T00:00:00+00:00",
                  "sourceInputsHash": "guid-hash",
                  "entries": [
                    {
                      "assetGuid": "a-guid",
                      "assetPath": "Assets/A.asset"
                    },
                    {
                      "assetGuid": "z-guid",
                      "assetPath": "Assets/Z.asset"
                    }
                  ]
                }
                """),
            Write(guidPathLookup));
        AssertExactJson(
            ExpectedJson(
                """
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-03-03T00:00:00+00:00",
                  "scenePath": "Assets/Scenes/Main.unity",
                  "sourceInputsHash": "scene-hash",
                  "roots": [
                    {
                      "name": "RootZ",
                      "globalObjectId": "z",
                      "children": []
                    },
                    {
                      "name": "RootA",
                      "globalObjectId": "a",
                      "children": []
                    }
                  ]
                }
                """),
            Write(sceneTreeLiteLookup));
        AssertExactJson(
            ExpectedJson(
                """
                {
                  "schemaVersion": 1,
                  "generatedAtUtc": "2026-03-03T00:00:00+00:00",
                  "scriptAssembliesHash": "script",
                  "packagesManifestHash": "manifest",
                  "packagesLockHash": "lock",
                  "assemblyDefinitionHash": "asmdef",
                  "assetsContentHash": "assets",
                  "assetSearchHash": "asset",
                  "guidPathHash": "guid",
                  "combinedHash": "combined"
                }
                """),
            Write(inputsManifest));
    }

    private static string Write (IndexTypesCatalogJsonContract contract)
    {
        return new IndexTypesCatalogJsonContractWriter().Write(contract);
    }

    private static string Write (IndexSchemasCatalogJsonContract contract)
    {
        return new IndexSchemasCatalogJsonContractWriter().Write(contract);
    }

    private static string Write (IndexOpsCatalogJsonContract contract)
    {
        return new IndexOpsCatalogJsonContractWriter().Write(contract);
    }

    private static string Write (IndexInputsManifestJsonContract contract)
    {
        return new IndexInputsManifestJsonContractWriter().Write(contract);
    }

    private static string Write (IndexAssetSearchLookupJsonContract contract)
    {
        return new IndexAssetSearchLookupJsonContractWriter().Write(contract);
    }

    private static string Write (IndexGuidPathLookupJsonContract contract)
    {
        return new IndexGuidPathLookupJsonContractWriter().Write(contract);
    }

    private static string Write (IndexSceneTreeLiteLookupJsonContract contract)
    {
        return new IndexSceneTreeLiteLookupJsonContractWriter().Write(contract);
    }

    private static UcliOperationDescribeContract CreateGoDescribeContract ()
    {
        return UcliOperationDescribeContractBuilder.Create<GoDescribeArgs, GameObjectDescriptionResult>(
            "Returns a GameObject description including components and child hierarchy.",
            new UcliOperationAssuranceContract(
                Array.Empty<UcliOperationSideEffect>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanMode.ObservesLiveUnity));
    }
}
