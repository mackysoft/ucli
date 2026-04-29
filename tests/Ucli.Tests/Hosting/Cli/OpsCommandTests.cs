using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Features.OperationCatalog.UseCases.Ops;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class OpsCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_MapsOptionsToOpsServiceInput ()
    {
        var service = new StubOpsService();
        var command = new OpsListCommand(service);

        await StandardOutputCapture.Execute(() => command.List(
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "1234",
            readIndexMode: "allowStale",
            failFast: true,
            cancellationToken: CancellationToken.None));

        var input = Assert.IsType<OpsCommandInput>(service.LastListInput);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal(UnityExecutionMode.Daemon, input.Mode);
        Assert.Equal(1234, input.TimeoutMilliseconds);
        Assert.Equal(ReadIndexMode.AllowStale, input.ReadIndexMode);
        Assert.True(input.FailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Describe_MapsOptionsToOpsServiceInput ()
    {
        var service = new StubOpsService();
        var command = new OpsDescribeCommand(service);

        await StandardOutputCapture.Execute(() => command.Describe(
            operationName: "ucli.go.describe",
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "1234",
            readIndexMode: "requireFresh",
            failFast: true,
            cancellationToken: CancellationToken.None));

        var input = Assert.IsType<OpsDescribeCommandInput>(service.LastDescribeInput);
        Assert.Equal("ucli.go.describe", input.OperationName);
        Assert.Equal("/repo/UnityProject", input.ProjectPath);
        Assert.Equal(UnityExecutionMode.Daemon, input.Mode);
        Assert.Equal(1234, input.TimeoutMilliseconds);
        Assert.Equal(ReadIndexMode.RequireFresh, input.ReadIndexMode);
        Assert.True(input.FailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubOpsService();
        var command = new OpsListCommand(service);

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.List(
            mode: "unsupported",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.LastListInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.OpsList,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
    }

    private sealed class StubOpsService : IOpsService
    {
        private static readonly JsonElement EmptySchema = JsonDocument.Parse("""{"type":"object"}""").RootElement.Clone();

        public OpsCommandInput? LastListInput { get; private set; }

        public OpsDescribeCommandInput? LastDescribeInput { get; private set; }

        public ValueTask<OpsListServiceResult> GetAll (
            OpsCommandInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastListInput = input;
            return ValueTask.FromResult(OpsListServiceResult.Success(
                new OpsListExecutionOutput(
                    Operations: [],
                    ReadIndex: new ReadIndexInfo(false, false, "index", "probable", null, null)),
                "uCLI ops list completed."));
        }

        public ValueTask<OpsDescribeServiceResult> Describe (
            OpsDescribeCommandInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastDescribeInput = input;
            return ValueTask.FromResult(OpsDescribeServiceResult.Success(
                new OpsDescribeExecutionOutput(
                    Operation: new OpsOperationDetail(
                        Name: "ucli.go.describe",
                        Kind: "query",
                        Policy: "safe",
                        ArgsSchema: EmptySchema),
                    ReadIndex: new ReadIndexInfo(false, false, "index", "probable", null, null)),
                "uCLI ops describe completed."));
        }
    }
}
