using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Ops.Mapping;

public sealed class OpsDescribeResultMapperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Map_WhenResultContractIsPresent_ReturnsGeneratedContracts ()
    {
        var mapper = new OpsDescribeResultMapper(new OpsReadIndexInfoMapper());
        var entry = CreateDescribedEntry(
            name: UcliPrimitiveOperationNames.Resolve,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe);

        var result = mapper.Map(CreateReadOutput(entry));

        var succeeded = Assert.IsType<OpsDescribeServiceResult.Succeeded>(result);
        var operation = succeeded.Output.Operation;
        var expectedArgsContract = Assert.IsType<UcliOperationJsonContract>(entry.ArgsContract);
        var expectedResultContract = Assert.IsType<UcliOperationJsonContract>(entry.ResultContract);
        var actualResultContract = Assert.IsType<UcliOperationJsonContract>(operation.ResultContract);
        Assert.Equal(expectedArgsContract.ContractDigest, operation.ArgsContract.ContractDigest);
        Assert.Equal(expectedResultContract.ContractDigest, actualResultContract.ContractDigest);
        Assert.Equal(UcliOperationPlayModeSupport.Disallowed, operation.PlayModeSupport);
        Assert.Equal(entry.DescriptorDigest, operation.DescriptorDigest);
        Assert.Equal(entry.VerdictContract, operation.VerdictContract);
        Assert.Equal("Resolves an asset, scene object, prefab object, or component reference to a Unity GlobalObjectId.", operation.Description);
    }

    private static OpsDescribeReadOutput CreateReadOutput (IndexOpEntryJsonContract entry)
    {
        return new OpsDescribeReadOutput(
            Operation: OperationCatalogTestFixtures.CreateValidatedOperation(entry),
            AccessInfo: new OpsCatalogAccessInfo(
                true,
                true,
                OpsCatalogSource.Index,
                MackySoft.Ucli.Contracts.Index.IndexFreshness.Fresh,
                DateTimeOffset.UtcNow,
                null));
    }

    private static IndexOpEntryJsonContract CreateDescribedEntry (
        string name,
        UcliOperationKind kind,
        OperationPolicy policy)
    {
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            name,
            serializerOptions.GetTypeInfo(typeof(ResolveSelectorArgs)),
            serializerOptions.GetTypeInfo(typeof(IpcResolveOperationResult)));
        var assurance = CreateAssurance(kind, policy);
        var verdictContract = kind == UcliOperationKind.Query
            ? new UcliOperationVerdictContract(
                "The supplied reference resolves to one Unity object.")
            : null;
        var describe = verdictContract == null
            ? UcliOperationDescribeContractBuilder.CreateWithoutVerdict(
                generationResult,
                "Resolves an asset, scene object, prefab object, or component reference to a Unity GlobalObjectId.",
                assurance,
                codeContract: null)
            : UcliOperationDescribeContractBuilder.CreateJudging(
                generationResult,
                "Resolves an asset, scene object, prefab object, or component reference to a Unity GlobalObjectId.",
                assurance,
                verdictContract,
                codeContract: null);
        var entry = new IndexOpEntryJsonContract(
            Name: name,
            Kind: kind,
            Policy: policy,
            ArgsContract: describe.ArgsContract,
            DescriptorDigest: null,
            ResultContract: describe.ResultContract,
            VerdictContract: describe.VerdictContract,
            Exposure: null,
            PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
        {
            Description = describe.Description,
            Assurance = describe.Assurance,
        };
        return entry with
        {
            DescriptorDigest = UcliOperationDescriptorDigest.Calculate(entry),
        };
    }

    private static UcliOperationAssuranceContract CreateAssurance (
        UcliOperationKind kind,
        OperationPolicy policy)
    {
        var isMutation = kind == UcliOperationKind.Mutation;
        var isDangerousPolicy = policy == OperationPolicy.Dangerous;
        var isRiskyPolicy = policy != OperationPolicy.Safe;
        return new UcliOperationAssuranceContract(
            sideEffects: isDangerousPolicy
                ? [UcliOperationSideEffect.AssetSave, UcliOperationSideEffect.ArbitrarySourceExecution]
                : isMutation ? [UcliOperationSideEffect.AssetSave] : [UcliOperationSideEffect.ObservesUnityState],
            touchedKinds: isMutation ? [UcliTouchedResourceKind.Asset] : Array.Empty<UcliTouchedResourceKind>(),
            planMode: UcliOperationPlanMode.ObservesLiveUnity,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: isMutation ? "Execute the mutation against live Unity state." : "Read Unity state without applying mutation.",
            touchedContract: isMutation ? "Reports the resource touched by the mutation." : "Returns no touched resources.",
            readPostconditionContract: isMutation ? "Touched resource read surfaces may be stale after a successful call." : "Does not stale read surfaces by itself.",
            failureSemantics: isMutation ? "Failure may leave partial or indeterminate Unity state changes." : "Failure means the observation was not fully produced.",
            dangerousNotes: isRiskyPolicy ? ["Fixture operation has policy-specific risk metadata for contract validation."] : Array.Empty<string>());
    }

}
