using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.Cli.OpsCommandTestSupport;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class OpsCommandDescribeOutputTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Describe_WhenServiceReturnsQueryOperation_MatchesQueryGolden ()
    {
        var service = CreateService();
        service.DescribeResult = CreateDescribeSuccess(
            operationName: UcliPrimitiveOperationNames.Resolve,
            kind: "query",
            policy: "safe",
            description: "Resolves a Unity object reference.",
            inputs: [],
            resultContract: new UcliOperationResultContract(
                emitted: true,
                resultType: "QueryResult",
                description: "Query result."),
            assurance: OpsCliOutputContractTestSupport.CreateAssurance("query", "safe"),
            argsSchemaJson: """{"type":"object"}""",
            resultSchemaJson: """{"type":"object"}""");
        var command = new OpsDescribeCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() =>
            command.DescribeAsync(UcliPrimitiveOperationNames.Resolve, cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ops", "describe-query-success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Describe_WhenServiceReturnsNoResultOperation_MatchesNoResultGolden ()
    {
        var service = CreateService();
        service.DescribeResult = CreateDescribeSuccess(
            operationName: UcliPrimitiveOperationNames.SceneOpen,
            kind: "command",
            policy: "safe",
            description: "Opens a Unity scene asset in the editor.",
            inputs: [],
            resultContract: UcliOperationResultContract.NoResult("No operation-specific result is emitted."),
            assurance: OpsCliOutputContractTestSupport.CreateAssurance("command", "safe"),
            argsSchemaJson: """{"type":"object"}""",
            resultSchemaJson: null);
        var command = new OpsDescribeCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() =>
            command.DescribeAsync(UcliPrimitiveOperationNames.SceneOpen, cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ops", "describe-no-result-success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Describe_WhenServiceReturnsVariantInputs_MatchesVariantGolden ()
    {
        const string operationName = "custom.variant.describe";

        var service = CreateService();
        service.DescribeResult = CreateDescribeSuccess(
            operationName: operationName,
            kind: "query",
            policy: "safe",
            description: "Returns a GameObject description including components and child hierarchy.",
            inputs: CreateVariantInputs(),
            resultContract: new UcliOperationResultContract(
                emitted: true,
                resultType: "GameObjectDescriptionResult",
                description: "GameObject describe operation result."),
            assurance: OpsCliOutputContractTestSupport.CreateAssurance("query", "safe"),
            argsSchemaJson:
                """
                {"type":"object","additionalProperties":false,"required":["target"],"properties":{"target":{"type":"object"},"depth":{"type":["integer","null"]}}}
                """,
            resultSchemaJson: """{"type":"object"}""");
        var command = new OpsDescribeCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() =>
            command.DescribeAsync(operationName, cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ops", "describe-variant-success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());
    }
}
