using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Contracts.Tests.Index;

internal static class IndexOpsDescribeJsonContractTestSupport
{
    public static IndexOpsDescribeJsonContract CreateGoDescribeIndexContract ()
    {
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: IndexOpsDescribeContractTestData.CreateGoDescribeOperation());
    }

    public static IndexOpsDescribeJsonContract CreateCodeOperationIndexContract ()
    {
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "hash",
            Operation: IndexOpsDescribeContractTestData.CreateCodeOperation());
    }
}
