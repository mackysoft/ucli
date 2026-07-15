using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Tests;

internal static class ReadIndexOperationTestFactory
{
    public static IndexOpEntryJsonContract CreateGoDescribeEntry (
        string argsSchemaJson = """{"type":"object"}""",
        IReadOnlyList<UcliOperationSideEffect>? sideEffects = null)
    {
        return new IndexOpEntryJsonContract(
            Name: UcliPrimitiveOperationNames.GoDescribe,
            Kind: "query",
            Policy: "safe",
            ArgsSchemaJson: argsSchemaJson,
            ResultSchemaJson: """{"type":"object"}""")
        {
            Description = "Returns a GameObject description including components and child hierarchy.",
            Inputs = Array.Empty<UcliOperationInputContract>(),
            ResultContract = UcliOperationResultContract.One<GameObjectDescriptionResult>("GameObject description result."),
            Assurance = CreateSafeQueryAssurance(sideEffects ?? Array.Empty<UcliOperationSideEffect>()),
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
