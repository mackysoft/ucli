using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Index;

internal static class IndexOpsDescribeContractTestData
{
    public static UcliOperationDescribeContract CreateGoDescribeContract ()
    {
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            UcliPrimitiveOperationNames.GoDescribe,
            serializerOptions.GetTypeInfo(typeof(GoDescribeArgs)),
            serializerOptions.GetTypeInfo(typeof(GameObjectDescriptionResult)));
        return UcliOperationDescribeContractBuilder.Create(
            generationResult,
            "Returns a GameObject description including components and child hierarchy.",
            new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate arguments and observe Unity state without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>()));
    }
}
