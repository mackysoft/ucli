using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Index;

internal static class IndexOpsDescribeJsonContractTestSupport
{
    public static IndexOpsDescribeJsonContract CreateGoDescribeIndexContract ()
    {
        var describe = IndexOpsDescribeContractTestData.CreateGoDescribeContract();
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: WithDescriptorDigest(
                new IndexOpEntryJsonContract(
                    Name: UcliPrimitiveOperationNames.GoDescribe,
                    Kind: UcliOperationKind.Query,
                    Policy: OperationPolicy.Safe,
                    ArgsContract: describe.ArgsContract,
                    DescriptorDigest: null,
                    ResultContract: describe.ResultContract,
                    VerdictContract: describe.VerdictContract,
                    Exposure: null,
                    PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
                {
                    Description = describe.Description,
                    Assurance = describe.Assurance,
                }));
    }

    public static IndexOpEntryJsonContract WithDescriptorDigest (IndexOpEntryJsonContract operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var descriptorWithoutDigest = operation with { DescriptorDigest = null };
        return descriptorWithoutDigest with
        {
            DescriptorDigest = UcliOperationDescriptorDigest.Calculate(descriptorWithoutDigest),
        };
    }

}
