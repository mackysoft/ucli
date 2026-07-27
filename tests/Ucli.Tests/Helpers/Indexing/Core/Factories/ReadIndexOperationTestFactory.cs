using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Tests;

internal static class ReadIndexOperationTestFactory
{
    public static IndexOpEntryJsonContract CreateGoDescribeEntry (
        IReadOnlyList<UcliOperationSideEffect>? sideEffects = null)
    {
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            UcliPrimitiveOperationNames.GoDescribe,
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(GoDescribeArgs)),
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(GameObjectDescriptionResult)));
        var describe = UcliOperationDescribeContractBuilder.Create(
            generationResult,
            "Returns a GameObject description including components and child hierarchy.",
            CreateSafeQueryAssurance(sideEffects ?? Array.Empty<UcliOperationSideEffect>()));

        return new IndexOpEntryJsonContract(
            Name: UcliPrimitiveOperationNames.GoDescribe,
            Kind: UcliOperationKind.Query,
            Policy: OperationPolicy.Safe,
            ArgsContract: describe.ArgsContract!,
            ResultContract: describe.ResultContract)
        {
            Description = describe.Description,
            Assurance = describe.Assurance,
        };
    }

    private static UcliOperationAssuranceContract CreateSafeQueryAssurance (IReadOnlyList<UcliOperationSideEffect> sideEffects)
    {
        return new UcliOperationAssuranceContract(
            sideEffects: sideEffects,
            touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
            planMode: UcliOperationPlanMode.ObservesLiveUnity,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: "Read Unity state without applying mutation.",
            touchedContract: "Returns no touched resources.",
            readPostconditionContract: "Does not stale read surfaces by itself.",
            failureSemantics: "Failure means the observation was not fully produced.",
            dangerousNotes: Array.Empty<string>());
    }
}
