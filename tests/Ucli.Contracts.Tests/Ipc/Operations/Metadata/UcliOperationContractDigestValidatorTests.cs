using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationContractDigestValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenGeneratedContractsHaveCanonicalDigests_ReturnsTrue ()
    {
        var noResult = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        var emittedResult = CreateEmittedResultDescribeContract();

        var noResultValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
            noResult,
            operationKind: UcliOperationKind.Command,
            operationPolicy: OperationPolicy.Safe,
            ownerName: "No-result operation",
            out var noResultError);
        var emittedResultValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
            emittedResult,
            operationKind: UcliOperationKind.Command,
            operationPolicy: OperationPolicy.Safe,
            ownerName: "Emitted-result operation",
            out var emittedResultError);

        Assert.True(noResultValid, noResultError);
        Assert.True(emittedResultValid, emittedResultError);
    }

    private static UcliOperationDescribeContract CreateEmittedResultDescribeContract ()
    {
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            "ucli.test.assets.find",
            serializerOptions.GetTypeInfo(typeof(AssetsFindArgs)),
            serializerOptions.GetTypeInfo(typeof(AssetsFindResult)));
        return UcliOperationDescribeContractBuilder.CreateWithoutVerdict(
            generationResult,
            "Finds project assets by type, path prefix, or name substring.",
            UcliOperationDescribeContractValidatorTestData.CreateAssurance(
                Array.Empty<UcliOperationSideEffect>(),
                Array.Empty<UcliTouchedResourceKind>(),
                UcliOperationPlanMode.ObservesLiveUnity,
                Array.Empty<string>()),
            codeContract: null);
    }
}
