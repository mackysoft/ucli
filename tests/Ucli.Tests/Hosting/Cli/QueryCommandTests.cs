using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Requests;

namespace MackySoft.Ucli.Tests;

public sealed class QueryCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task AssetsFind_UsesQueryServiceAndWritesCommandResult ()
    {
        var service = new StubQueryService((_, _) => ValueTask.FromResult(CreateSuccessResult(UcliCommandNames.QueryAssetsFind)));
        var command = new QueryAssetsFindCommand(service);
        using var cancellationTokenSource = new CancellationTokenSource();

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Find(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            readIndexMode: "allowStale",
            failFast: true,
            type: "UnityEngine.Material, UnityEngine.CoreModule",
            limit: 50,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        Assert.NotNull(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", service.CapturedInput!.ProjectPath);
        Assert.Equal(UnityExecutionMode.Oneshot, service.CapturedInput.Mode);
        Assert.Equal(1234, service.CapturedInput.TimeoutMilliseconds);
        Assert.Equal(ReadIndexMode.AllowStale, service.CapturedInput.ReadIndexMode);
        Assert.True(service.CapturedInput.FailFast);

        var operation = Assert.IsType<QueryAssetsFindOperationRequest>(service.CapturedInput.Operation);
        Assert.Equal(UcliCommandNames.QueryAssetsFind, operation.CommandName);
        Assert.Equal("assets.find", operation.OperationId);
        Assert.Equal(UcliPrimitiveOperationNames.AssetsFind, operation.OperationName);
        Assert.Equal("UnityEngine.Material, UnityEngine.CoreModule", operation.Filter.TypeId);
        Assert.Equal(50, operation.WindowOptions.Limit);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.QueryAssetsFind,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "uCLI query completed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                .HasArrayLength("opResults", 1)
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", true)
                    .HasString("source", "index")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task AssetsFind_WhenWindowOptionsConflict_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubQueryService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new QueryAssetsFindCommand(service);

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Find(
            type: "UnityEngine.Material, UnityEngine.CoreModule",
            limit: 10,
            all: true,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.QueryAssetsFind,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GoDescribe_WhenTargetIsAmbiguous_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubQueryService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new QueryGoDescribeCommand(service);

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Describe(
            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
            scene: "Assets/Scenes/Main.unity",
            hierarchyPath: "Root",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.QueryGoDescribe,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AssetsGroup_WhenLeafSubcommandIsMissing_ReturnsJsonInvalidArgument ()
    {
        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Query,
            UcliCommandNames.AssetsSubcommand);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Query,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "Subcommand is required for command 'query assets'. Supported subcommands: find.");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AssetsFind_WithCamelCaseAliases_IsAcceptedByParser ()
    {
        var invalidProjectPath = Path.Combine(Path.GetTempPath(), $"ucli-query-missing-{Guid.NewGuid():N}");

        var result = await CliProcessRunner.RunCommand(
            UcliCommandNames.Query,
            UcliCommandNames.AssetsSubcommand,
            UcliCommandNames.FindSubcommand,
            "--type",
            "UnityEngine.Material, UnityEngine.CoreModule",
            UcliContractConstants.CliOption.ProjectPath,
            invalidProjectPath,
            UcliContractConstants.CliOption.FailFast);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.DoesNotContain("Argument '--projectPath' is not recognized.", result.StdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("Argument '--failFast' is not recognized.", result.StdErr, StringComparison.Ordinal);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.QueryAssetsFind,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    private static QueryServiceResult CreateSuccessResult (string commandName)
    {
        return new QueryServiceResult(
            CommandName: commandName,
            RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            OpResults:
            [
                new IpcExecuteOperationResult(
                    OpId: "assets.find",
                    Op: UcliPrimitiveOperationNames.AssetsFind,
                    Phase: IpcExecuteOperationPhaseNames.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: [])
                {
                    Result = JsonSerializer.SerializeToElement(new
                    {
                        matches = Array.Empty<object>(),
                    }),
                },
            ],
            Errors: [],
            Outcome: ApplicationOutcome.Success,
            Message: "uCLI query completed.",
            ReadIndex: new ReadIndexInfo(
                Used: true,
                Hit: true,
                Source: ReadIndexInfoSource.Index,
                Freshness: IndexFreshness.Fresh,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                FallbackReason: null));
    }

    private sealed class StubQueryService : IQueryService
    {
        private readonly Func<QueryCommandInput, CancellationToken, ValueTask<QueryServiceResult>> handler;

        public StubQueryService (Func<QueryCommandInput, CancellationToken, ValueTask<QueryServiceResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public QueryCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<QueryServiceResult> Execute (
            QueryCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}
