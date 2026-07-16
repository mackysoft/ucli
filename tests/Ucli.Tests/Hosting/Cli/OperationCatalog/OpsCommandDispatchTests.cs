using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.Cli.OpsCommandTestSupport;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class OpsCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WithSupportedFilters_DispatchesOpsListQueryWithResolvedOptions ()
    {
        var service = CreateService();
        var command = new OpsListCommand(service, CommandResultTestWriter.Create());

        await StandardOutputCapture.ExecuteAsync(() => command.ListAsync(
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "1234",
            readIndexMode: "allowStale",
            nameRegex: "^ucli\\.",
            operationKind: "mutation",
            maxPolicy: "advanced",
            failFast: true,
            cancellationToken: CancellationToken.None));

        OpsCommandAssert.ListOptionsDispatchedAsOpsQuery(
            service,
            expectedProjectPath: "/repo/UnityProject",
            expectedMode: UnityExecutionMode.Daemon,
            expectedTimeoutMilliseconds: 1234,
            expectedReadIndexMode: ReadIndexMode.AllowStale,
            expectedNameRegex: "^ucli\\.",
            expectedOperationKind: UcliOperationKind.Mutation,
            expectedMaxPolicy: OperationPolicy.Advanced,
            expectedFailFast: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Describe_WithSupportedOptions_DispatchesOpsDescribeQueryWithResolvedOptions ()
    {
        var service = CreateService();
        var command = new OpsDescribeCommand(service, CommandResultTestWriter.Create());

        await StandardOutputCapture.ExecuteAsync(() => command.DescribeAsync(
            operationName: "ucli.go.describe",
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "1234",
            readIndexMode: "requireFresh",
            failFast: true,
            cancellationToken: CancellationToken.None));

        OpsCommandAssert.DescribeOptionsDispatchedAsOpsQuery(
            service,
            expectedOperationName: "ucli.go.describe",
            expectedProjectPath: "/repo/UnityProject",
            expectedMode: UnityExecutionMode.Daemon,
            expectedTimeoutMilliseconds: 1234,
            expectedReadIndexMode: ReadIndexMode.RequireFresh,
            expectedFailFast: true);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task List_WhenModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = CreateService();
        var command = new OpsListCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ListAsync(
            mode: "unsupported",
            cancellationToken: CancellationToken.None));

        OpsCommandAssert.InvalidListInputRejectedBeforeOpsExecution(
            result,
            service,
            "list-invalid-mode.json");
    }
}
