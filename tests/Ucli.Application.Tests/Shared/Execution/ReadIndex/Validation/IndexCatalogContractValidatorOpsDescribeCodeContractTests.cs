using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

public sealed class IndexCatalogContractValidatorOpsDescribeCodeContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsValidOpsDescribe_ReturnsTrue_WhenDescribeContractHasCodeContract ()
    {
        var entry = IndexCatalogContractValidatorOpsTestSupport.CreateValidOpsEntry() with
        {
            Policy = "dangerous",
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: [UcliOperationSideEffect.ArbitrarySourceExecution],
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanMode.ValidationOnly,
                planSemantics: "Validate code without applying mutation.",
                callSemantics: "Execute caller-provided source code.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Source execution may stale read surfaces.",
                failureSemantics: "Execution failure may leave indeterminate process state.",
                dangerousNotes: ["Executes caller-provided source code."]),
            CodeContract = new UcliOperationCodeContract(
                "csharp",
                new UcliCodeEntryPointContract(
                    "public static object? Run(UcliCsEvalContext context)",
                    "Compiled source must contain exactly one matching Run method.",
                    requiredStatic: true,
                    new[] { "MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext" },
                    "JSON-serializable value."),
                new[]
                {
                    new UcliCodeSourceFormContract(CsEvalSourceKindValues.CompilationUnit, "Complete C# compilation unit."),
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

        var result = IndexCatalogContractValidator.IsValidOpsDescribe(contract);

        Assert.True(result);
    }
}
