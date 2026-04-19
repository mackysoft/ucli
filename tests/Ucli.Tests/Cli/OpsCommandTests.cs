using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Ops;
using MackySoft.Ucli.ReadIndex;

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
        Assert.Equal("daemon", input.Mode);
        Assert.Equal("1234", input.Timeout);
        Assert.Equal("allowStale", input.ReadIndexMode);
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
        Assert.Equal("daemon", input.Mode);
        Assert.Equal("1234", input.Timeout);
        Assert.Equal("requireFresh", input.ReadIndexMode);
        Assert.True(input.FailFast);
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