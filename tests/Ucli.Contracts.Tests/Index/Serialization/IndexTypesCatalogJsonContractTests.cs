using MackySoft.Ucli.Contracts.Index;
using static MackySoft.Tests.JsonTextAssert;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexTypesCatalogJsonContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serializer_RoundTripsCatalogEntry ()
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

        var json = new IndexTypesCatalogJsonContractWriter().Write(contract);
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
    public void Writer_OrdersEntriesByTypeIdAndUsesStableContractFields ()
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

        var json = new IndexTypesCatalogJsonContractWriter().Write(contract);

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
}
