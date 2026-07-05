using MackySoft.Ucli.Contracts.Index;
using static MackySoft.Tests.JsonTextAssert;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexGuidPathLookupJsonContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serializer_RoundTripsGuidPathEntries ()
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

        var json = new IndexGuidPathLookupJsonContractWriter().Write(contract);
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
    public void Writer_OrdersEntriesByAssetPathAndUsesStableContractFields ()
    {
        var contract = new IndexGuidPathLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
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
            new IndexGuidPathLookupJsonContractWriter().Write(contract));
    }
}
