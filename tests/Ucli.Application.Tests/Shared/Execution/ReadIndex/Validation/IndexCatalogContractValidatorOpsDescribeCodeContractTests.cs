namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorOpsDescribeCodeContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryCreateOpsDescribeSnapshot_ReturnsTrue_WhenDescribeContractHasCodeContract ()
    {
        var entry = IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry() with
        {
            Policy = "dangerous",
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: [UcliOperationSideEffect.ArbitrarySourceExecution],
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ValidationOnly,
                planSemantics: "Validate code without applying mutation.",
                callSemantics: "Execute caller-provided source code.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Source execution may stale read surfaces.",
                failureSemantics: "Execution failure may leave indeterminate process state.",
                dangerousNotes: ["Executes caller-provided source code."]),
            CodeContract = new UcliOperationCodeContract(
                UcliCodeLanguage.CSharp,
                new UcliCodeEntryPointContract(
                    "public static object? | Task | Task<T> | ValueTask | ValueTask<T> Run(UcliCsEvalContext context)",
                    "Compiled source must contain exactly one public static Run(UcliCsEvalContext context) method returning object?, Task, Task<T>, ValueTask, or ValueTask<T>.",
                    requiredStatic: true,
                    new[] { "MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext" },
                    "JSON-serializable value or awaited task-like result."),
                new[]
                {
                    new UcliCodeSourceFormContract(UcliCodeSourceFormKind.CompilationUnit, "Complete C# compilation unit."),
                },
                new[]
                {
                    new UcliCodeApiTypeContract(
                        "UcliCsEvalContext",
                        "MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext",
                        "Execution context.",
                        Array.Empty<UcliCodeApiMemberContract>()),
                }),
        };
        var contract = IndexCatalogContractValidatorOpsTestSupport.CreateOpsDescribe(entry);

        var result = OpsDescribeSnapshot.TryCreate(contract, out var snapshot);

        Assert.True(result);
        Assert.NotNull(snapshot);
    }
}
