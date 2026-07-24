using System.Text.Json;
using MackySoft.Ucli.Tests.Helpers.Assurance;

namespace MackySoft.Ucli.Tests;

public sealed class CliOutputGoldenAssuranceSemanticContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void AssuranceCommandGoldens_WithOkStatus_SatisfyCommonShapeAndSemanticInvariants ()
    {
        foreach (CliOutputGoldenFiles.GoldenDocument golden in CliOutputGoldenFiles.ReadAllDocuments())
        {
            CliOutputGoldenContractTestSupport.AssertGolden(
                golden,
                AssertAssuranceCommandGoldenSatisfiesSemanticInvariants);
        }
    }

    private static void AssertAssuranceCommandGoldenSatisfiesSemanticInvariants (JsonElement root)
    {
        var command = CliOutputGoldenContractTestSupport.ReadRequiredString(root, "command", "$.command");
        if (!IsAssuranceCommand(command)
            || !string.Equals(
                CliOutputGoldenContractTestSupport.ReadRequiredString(root, "status", "$.status"),
                TextVocabulary.GetText(CommandResultStatus.Ok),
                StringComparison.Ordinal))
        {
            return;
        }

        var payload = CliOutputGoldenContractTestSupport.ReadRequiredObject(root, "payload", "$.payload");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(payload, "verdict", JsonValueKind.String, "$.payload.verdict");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(payload, "project", JsonValueKind.Object, "$.payload.project");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(payload, "verifiers", JsonValueKind.Array, "$.payload.verifiers");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(payload, "claims", JsonValueKind.Array, "$.payload.claims");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(payload, "reports", JsonValueKind.Object, "$.payload.reports");
        CliOutputGoldenContractTestSupport.AssertPropertyKind(payload, "residualRisks", JsonValueKind.Array, "$.payload.residualRisks");

        var result = CliAssuranceSemanticInvariantValidatorFactory.CreateAllAssuranceCommandValidator().Validate(payload);

        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
    }

    private static bool IsAssuranceCommand (string command)
    {
        return string.Equals(command, "ready", StringComparison.Ordinal)
            || string.Equals(command, "compile", StringComparison.Ordinal)
            || string.Equals(command, "build.run", StringComparison.Ordinal)
            || string.Equals(command, "verify", StringComparison.Ordinal);
    }
}
