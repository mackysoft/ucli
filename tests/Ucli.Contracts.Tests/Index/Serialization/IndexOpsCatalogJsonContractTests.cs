using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Tests.JsonTextAssert;

namespace MackySoft.Ucli.Contracts.Tests.Index;

public sealed class IndexOpsCatalogJsonContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serializer_RoundTripsOperationCatalogEntry ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexOpsCatalogEntryJsonContract(
                    Name: UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    Description: "Returns a GameObject description.",
                    DescribeKey: new string('a', 64),
                    DescribeHash: new string('b', 64)),
            ]);

        var json = new IndexOpsCatalogJsonContractWriter().Write(contract);
        var deserialized = IndexOpsCatalogJsonContractSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(contract.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(contract.SourceInputsHash, deserialized.SourceInputsHash);
        Assert.NotNull(deserialized.Entries);
        Assert.Single(deserialized.Entries);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, deserialized.Entries[0].Name);
        Assert.Equal("query", deserialized.Entries[0].Kind);
        Assert.Equal("safe", deserialized.Entries[0].Policy);
        Assert.Equal("Returns a GameObject description.", deserialized.Entries[0].Description);
        Assert.Equal(new string('a', 64), deserialized.Entries[0].DescribeKey);
        Assert.Equal(new string('b', 64), deserialized.Entries[0].DescribeHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Writer_OrdersEntriesByNameAndUsesStableContractFields ()
    {
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Entries:
            [
                new IndexOpsCatalogEntryJsonContract(
                    Name: "z.op",
                    Kind: "mutation",
                    Policy: "dangerous",
                    Description: "Runs z operation.",
                    DescribeKey: new string('f', 64),
                    DescribeHash: new string('9', 64)),
                new IndexOpsCatalogEntryJsonContract(
                    Name: "a.op",
                    Kind: "query",
                    Policy: "safe",
                    Description: "Runs a operation.",
                    DescribeKey: new string('a', 64),
                    DescribeHash: new string('1', 64)),
            ]);

        var json = new IndexOpsCatalogJsonContractWriter().Write(contract);

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
                      "description": "Runs a operation.",
                      "describeKey": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                      "describeHash": "1111111111111111111111111111111111111111111111111111111111111111"
                    },
                    {
                      "name": "z.op",
                      "kind": "mutation",
                      "policy": "dangerous",
                      "description": "Runs z operation.",
                      "describeKey": "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                      "describeHash": "9999999999999999999999999999999999999999999999999999999999999999"
                    }
                  ]
                }
                """),
            json);
    }
}
