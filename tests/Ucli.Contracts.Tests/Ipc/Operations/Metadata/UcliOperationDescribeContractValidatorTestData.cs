using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

internal static class UcliOperationDescribeContractValidatorTestData
{
    public static UcliOperationDescribeContract CreateValidDescribeContract ()
    {
        return UcliOperationDescribeContractBuilder.Create<ScenePathArgs, UcliNoResult>(
            "Opens a Unity scene asset in the editor.",
            CreateAssurance(
                Array.Empty<UcliOperationSideEffect>(),
                Array.Empty<UcliTouchedResourceKind>(),
                UcliOperationPlanMode.ObservesLiveUnity,
                Array.Empty<string>()));
    }

    public static UcliOperationAssuranceContract CreateAssurance (
        IReadOnlyList<UcliOperationSideEffect> sideEffects,
        IReadOnlyList<UcliTouchedResourceKind> touchedKinds,
        UcliOperationPlanMode planMode,
        IReadOnlyList<string> dangerousNotes)
    {
        return new UcliOperationAssuranceContract(
            sideEffects,
            touchedKinds,
            planMode,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: "Execute the operation contract.",
            touchedContract: "Reports resources touched by the operation.",
            readPostconditionContract: "Reports read surfaces made stale by the operation.",
            failureSemantics: "Failure means the operation was not completed.",
            dangerousNotes);
    }

    public static UcliOperationCodeContract CreateValidCodeContract ()
    {
        return new UcliOperationCodeContract(
            UcliCodeLanguage.CSharp,
            new UcliCodeEntryPointContract(
                "public static object? Run(SampleContext context)",
                "Compiled source must contain exactly one matching Run method.",
                requiredStatic: true,
                new[] { "SampleContext" },
                "JSON-serializable value."),
            new[]
            {
                new UcliCodeSourceFormContract(UcliCodeSourceFormKind.CompilationUnit, "Complete C# compilation unit."),
            },
            new[]
            {
                new UcliCodeApiTypeContract(
                    "SampleContext",
                    "SampleContext",
                    "Sample context.",
                    new[]
                    {
                        new UcliCodeApiMemberContract(
                            UcliCodeApiMemberKind.Method,
                            "Log",
                            "Records a log message.",
                            type: null,
                            returnType: "void",
                            parameters:
                            [
                                new UcliCodeApiParameterContract("message", "System.String", "Log message."),
                            ]),
                    }),
            });
    }
}
