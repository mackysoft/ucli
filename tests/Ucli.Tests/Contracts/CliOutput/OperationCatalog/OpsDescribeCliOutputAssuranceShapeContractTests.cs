using System.Text.Json;

namespace MackySoft.Ucli.Tests;

public sealed class OpsDescribeCliOutputAssuranceShapeContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void OpsDescribeGoldens_AssuranceContractsExposeLatestShape ()
    {
        foreach (CliOutputGoldenFiles.GoldenDocument golden in CliOutputGoldenFiles.ReadAllDocuments())
        {
            CliOutputGoldenContractTestSupport.AssertGolden(
                golden,
                AssertOpsDescribeGoldenExposesLatestAssuranceShape);
        }
    }

    private static void AssertOpsDescribeGoldenExposesLatestAssuranceShape (JsonElement root)
    {
        if (!string.Equals(CliOutputGoldenContractTestSupport.ReadRequiredString(root, "command", "$.command"), "ops.describe", StringComparison.Ordinal)
            || !CliOutputGoldenContractTestSupport.TryGetProperty(root, "payload", JsonValueKind.Object, out var payload)
            || !CliOutputGoldenContractTestSupport.TryGetProperty(payload, "operation", JsonValueKind.Object, out var operation))
        {
            return;
        }

        var assurance = CliOutputGoldenContractTestSupport.ReadRequiredObject(operation, "assurance", "$.payload.operation.assurance");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(assurance, "sideEffects", JsonValueKind.Array, "$.payload.operation.assurance.sideEffects");
        CliOutputGoldenContractTestSupport.AssertBooleanProperty(assurance, "mayDirty", "$.payload.operation.assurance.mayDirty");
        CliOutputGoldenContractTestSupport.AssertBooleanProperty(assurance, "mayPersist", "$.payload.operation.assurance.mayPersist");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(assurance, "touchedKinds", JsonValueKind.Array, "$.payload.operation.assurance.touchedKinds");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(assurance, "planMode", JsonValueKind.String, "$.payload.operation.assurance.planMode");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(assurance, "planSemantics", JsonValueKind.String, "$.payload.operation.assurance.planSemantics");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(assurance, "callSemantics", JsonValueKind.String, "$.payload.operation.assurance.callSemantics");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(assurance, "touchedContract", JsonValueKind.String, "$.payload.operation.assurance.touchedContract");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(assurance, "readPostconditionContract", JsonValueKind.String, "$.payload.operation.assurance.readPostconditionContract");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(assurance, "failureSemantics", JsonValueKind.String, "$.payload.operation.assurance.failureSemantics");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(assurance, "dangerousNotes", JsonValueKind.Array, "$.payload.operation.assurance.dangerousNotes");
    }
}
