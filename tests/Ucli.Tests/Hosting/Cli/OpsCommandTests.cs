using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class OpsCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_MapsOptionsToOpsServiceInput ()
    {
        var service = new StubOpsService();
        var command = new OpsListCommand(service, CommandResultTestWriter.Create());

        await StandardOutputCapture.ExecuteAsync(() => command.ListAsync(
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
        var command = new OpsDescribeCommand(service, CommandResultTestWriter.Create());

        await StandardOutputCapture.ExecuteAsync(() => command.DescribeAsync(
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
        var command = new OpsListCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ListAsync(
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

        public ValueTask<OpsListServiceResult> GetAllAsync (
            OpsCommandInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastListInput = input;
            return ValueTask.FromResult(OpsListServiceResult.Success(
                new OpsListExecutionOutput(
                    Operations: [],
                    ReadIndex: new ReadIndexInfo(
                        false,
                        false,
                        ReadIndexInfoSource.Index,
                        IndexFreshness.Probable,
                        null,
                        null)),
                "uCLI ops list completed."));
        }

        public ValueTask<OpsDescribeServiceResult> DescribeAsync (
            OpsDescribeCommandInput input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastDescribeInput = input;
            var describe = CreateGoDescribeContract();
            return ValueTask.FromResult(OpsDescribeServiceResult.Success(
                new OpsDescribeExecutionOutput(
                    Operation: new OpsOperationDetail(
                        name: "ucli.go.describe",
                        kind: "query",
                        policy: "safe",
                        description: describe.Description!,
                        inputs: describe.Inputs!,
                        resultContract: describe.ResultContract!,
                        assurance: describe.Assurance!,
                        codeContract: describe.CodeContract,
                        argsSchema: EmptySchema,
                        resultSchema: null),
                    ReadIndex: new ReadIndexInfo(
                        false,
                        false,
                        ReadIndexInfoSource.Index,
                        IndexFreshness.Probable,
                        null,
                        null)),
                "uCLI ops describe completed."));
        }

        private static UcliOperationDescribeContract CreateGoDescribeContract ()
        {
            return UcliOperationDescribeContractBuilder.Create<GoDescribeArgs, GameObjectDescriptionResult>(
                "Returns a GameObject description including components and child hierarchy.",
                new UcliOperationAssuranceContract(
                    Array.Empty<UcliOperationSideEffect>(),
                    mayDirty: false,
                    mayPersist: false,
                    Array.Empty<string>(),
                    UcliOperationPlanMode.ObservesLiveUnity));
        }
    }
}
