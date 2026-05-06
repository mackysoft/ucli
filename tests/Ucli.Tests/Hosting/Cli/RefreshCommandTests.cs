using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class RefreshCommandTests
{
    private static readonly OperationExecuteResult SuccessResult = OperationExecuteResultFactory.Success(
        "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
        [
            new OperationExecutionOperationResult(
                OpId: "refresh",
                Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched:
                [
                    new OperationExecutionTouchedResource(
                        Kind: IpcExecuteTouchedResourceKindNames.Asset,
                        Path: "Assets/Example.txt",
                        Guid: null),
                ]),
        ],
        "uCLI refresh completed.");

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_UsesRefreshServiceAndWritesCommandResult ()
    {
        var service = new StubRefreshService((_, _) => ValueTask.FromResult(SuccessResult));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Refresh(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            failFast: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        Assert.NotNull(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", service.CapturedInput!.ProjectPath);
        Assert.Equal(UnityExecutionMode.Oneshot, service.CapturedInput.Mode);
        Assert.Equal(1234, service.CapturedInput.TimeoutMilliseconds);
        Assert.True(service.CapturedInput.FailFast);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "uCLI refresh completed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                .HasArrayLength("opResults", 1)
                .HasProperty("opResults", 0, op => op
                    .HasString("opId", "refresh")
                    .HasString("op", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh)
                    .HasString("phase", "call")
                    .HasBoolean("applied", true)
                    .HasBoolean("changed", true)
                    .HasArrayLength("touched", 1)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_WhenServiceFails_PreservesFailurePayloadAndErrors ()
    {
        var failureResult = OperationExecuteResultFactory.Failure(
            "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            [],
            [
                new OperationExecutionError(
                    IpcErrorCodes.InternalError,
                    "Unity execution failed.",
                    "refresh"),
            ],
            ApplicationOutcome.ToolError,
            "uCLI refresh failed.");
        var service = new StubRefreshService((_, _) => ValueTask.FromResult(failureResult));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Refresh(
            projectPath: "/repo/UnityProject",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, exitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "Unity execution failed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                .HasArrayLength("opResults", 0))
            .HasArrayLength("errors", 1)
            .HasProperty("errors", 0, error => error
                .HasString("code", IpcErrorCodes.InternalError)
                .HasString("message", "Unity execution failed.")
                .HasString("opId", "refresh"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_WhenReadPostconditionExists_WritesTopLevelPayload ()
    {
        var readPostcondition = new OperationExecutionReadPostcondition(
        [
            new OperationExecutionReadPostconditionRequirement(
                Surface: IpcExecuteReadPostconditionSurfaceNames.AssetSearch,
                MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T01:02:03+00:00")),
        ]);
        var service = new StubRefreshService((_, _) => ValueTask.FromResult(OperationExecuteResultFactory.Success(
            "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            [
                new OperationExecutionOperationResult(
                    OpId: "refresh",
                    Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                    Phase: IpcExecuteOperationPhaseNames.Call,
                    Applied: true,
                    Changed: true,
                    Touched: []),
            ],
            "uCLI refresh completed.",
            readPostcondition)));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Refresh(
            projectPath: "/repo/UnityProject",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("readPostcondition", readPostconditionElement => readPostconditionElement
                .HasArrayLength("requirements", 1)
                .HasProperty("requirements", 0, requirement => requirement
                    .HasString("surface", IpcExecuteReadPostconditionSurfaceNames.AssetSearch)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubRefreshService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Refresh(
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        AssertRefreshFailurePayload(outputJson.RootElement);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_WhenModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubRefreshService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Refresh(
            mode: "unsupported",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        AssertRefreshFailurePayload(outputJson.RootElement);
    }

    private sealed class StubRefreshService : IRefreshService
    {
        private readonly Func<RefreshCommandInput, CancellationToken, ValueTask<OperationExecuteResult>> handler;

        public StubRefreshService (Func<RefreshCommandInput, CancellationToken, ValueTask<OperationExecuteResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public RefreshCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<OperationExecuteResult> Execute (
            RefreshCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }

    private static void AssertRefreshFailurePayload (JsonElement rootElement)
    {
        var payload = rootElement.GetProperty("payload");
        var requestId = payload.GetProperty("requestId").GetString();

        Assert.True(Guid.TryParseExact(requestId, "D", out _));
        JsonAssert.For(payload)
            .HasArrayLength("opResults", 0);
    }
}
