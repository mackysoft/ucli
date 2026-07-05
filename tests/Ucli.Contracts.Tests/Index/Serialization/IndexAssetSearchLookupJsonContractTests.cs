using MackySoft.Ucli.Contracts.Index;
using static MackySoft.Tests.JsonTextAssert;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexAssetSearchLookupJsonContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serializer_RoundTripsAssetSearchEntries ()
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

        var json = new IndexAssetSearchLookupJsonContractWriter().Write(contract);
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
    public void Writer_OrdersEntriesByAssetPathAndUsesStableContractFields ()
    {
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
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
            new IndexAssetSearchLookupJsonContractWriter().Write(contract));
    }
}
