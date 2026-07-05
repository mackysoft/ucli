using MackySoft.Ucli.Contracts.Index;
using static MackySoft.Tests.JsonTextAssert;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexSchemasCatalogJsonContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serializer_RoundTripsCatalogEntryWithProperties ()
    {
        var contract = new IndexSchemasCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexSchemaEntryJsonContract(
                    SchemaKey: "comp:Game.Spawner, Assembly-CSharp",
                    Kind: "comp",
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    DisplayName: "Spawner",
                    Properties:
                    [
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "spawnInterval",
                            PropertyType: "float",
                            DeclaredTypeId: "System.Single, mscorlib",
                            IsArray: false,
                            ElementTypeId: null,
                            IsReadOnly: false),
                    ]),
            ]);

        var json = new IndexSchemasCatalogJsonContractWriter().Write(contract);
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
        Assert.Equal("float", properties[0].PropertyType);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Writer_OrdersEntriesAndPropertiesWithStableContractFields ()
    {
        var contract = new IndexSchemasCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries:
            [
                new IndexSchemaEntryJsonContract(
                    SchemaKey: "schema:z",
                    Kind: "asset",
                    TypeId: "Z.Type, Assembly-CSharp",
                    DisplayName: "Z",
                    Properties: Array.Empty<IndexSchemaPropertyEntryJsonContract>()),
                new IndexSchemaEntryJsonContract(
                    SchemaKey: "schema:a",
                    Kind: "comp",
                    TypeId: "A.Type, Assembly-CSharp",
                    DisplayName: "A",
                    Properties:
                    [
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "z",
                            PropertyType: "string",
                            DeclaredTypeId: "System.String, mscorlib",
                            IsArray: false,
                            ElementTypeId: null,
                            IsReadOnly: true),
                        new IndexSchemaPropertyEntryJsonContract(
                            Path: "a",
                            PropertyType: "float",
                            DeclaredTypeId: "System.Single, mscorlib",
                            IsArray: true,
                            ElementTypeId: "System.Single, mscorlib",
                            IsReadOnly: false),
                    ]),
            ]);

        var json = new IndexSchemasCatalogJsonContractWriter().Write(contract);

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
}
