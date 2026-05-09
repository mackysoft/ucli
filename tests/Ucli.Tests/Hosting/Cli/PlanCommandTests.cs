using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Input;
using MackySoft.Ucli.Hosting.Cli.Requests.Plan.Preflight;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlanCommandTests
{
    private const string DefaultRequestJson = """{"steps":[]}""";

    [Fact]
    [Trait("Size", "Small")]
    public async Task Plan_UsesPlanServiceAndWritesCommandResult ()
    {
        var service = new StubPlanService((input, _) => ValueTask.FromResult(PlanServiceResult.Success(
            new PlanExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                OpResults:
                [
                    new OperationExecutionOperationResult(
                        OpId: "step-1",
                        Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                        Phase: IpcExecuteOperationPhaseNames.Plan,
                        Applied: false,
                        Changed: false,
                        Touched: []),
                ],
                ReadIndex: CreateReadIndexInfo(
                    used: false,
                    hit: false,
                    fallbackReason: "readIndex disabled by mode."),
                PlanToken: "plan-token-1"),
            "uCLI plan completed.")));
        var preflightService = new StubPlanCommandPreflightService((_, _, _, _) => throw new InvalidOperationException("Preflight should not be called."));
        var command = new PlanCommand(service, preflightService, new StubRequestInputReader(RequestInputReadResult.Success(DefaultRequestJson)), CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.PlanAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            readIndexMode: "disabled",
            failFast: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        Assert.NotNull(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", service.CapturedInput!.ProjectPath);
        Assert.Equal(UnityExecutionMode.Oneshot, service.CapturedInput.Mode);
        Assert.Equal(1234, service.CapturedInput.TimeoutMilliseconds);
        Assert.Equal(ReadIndexMode.Disabled, service.CapturedInput.ReadIndexMode);
        Assert.True(service.CapturedInput.FailFast);
        Assert.Equal(DefaultRequestJson, service.CapturedInput.RequestJson);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62"));
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("plan", "success.json"),
            standardOutput,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Plan_WhenServiceFailsWithoutPlanToken_OmitsPlanTokenFromPayload ()
    {
        var service = new StubPlanService((_, _) => ValueTask.FromResult(PlanServiceResult.Failure(
            "Static validation failed.",
            [
                ApplicationFailure.InvalidInput(
                    "Operation args are invalid.",
                    ValidationErrorCodes.OperationArgsInvalid,
                    "step-1"),
            ],
            new PlanExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                OpResults: [],
                ReadIndex: CreateReadIndexInfo(
                    used: true,
                    hit: true,
                    fallbackReason: null),
                PlanToken: null))));
        var preflightService = new StubPlanCommandPreflightService((_, _, _, _) => throw new InvalidOperationException("Preflight should not be called."));
        var command = new PlanCommand(service, preflightService, new StubRequestInputReader(RequestInputReadResult.Success(DefaultRequestJson)), CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.PlanAsync(
            projectPath: "/repo/UnityProject",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Plan,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("planToken", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Plan_WhenReadIndexModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubPlanService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var preflightService = new StubPlanCommandPreflightService((_, _, _, _) => throw new InvalidOperationException("Preflight should not be called."));
        var command = new PlanCommand(service, preflightService, new StubRequestInputReader(RequestInputReadResult.Success(DefaultRequestJson)), CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.PlanAsync(
            readIndexMode: "unsupported",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Plan,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        Assert.Equal(0, preflightService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Plan_WhenTimeoutIsInvalid_UsesFeatureFailurePathWithoutExecutingPlan ()
    {
        var service = new StubPlanService((_, _) => throw new InvalidOperationException("Execute should not be called."));
        var preflightService = new StubPlanCommandPreflightService((_, _, _, _) => ValueTask.FromResult(PlanCommandPreflightResult.Success(
            new PlanExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                OpResults: [],
                ReadIndex: CreateReadIndexInfo(
                    used: false,
                    hit: false,
                    fallbackReason: "readIndex disabled by mode."),
                PlanToken: null))));
        var command = new PlanCommand(service, preflightService, new StubRequestInputReader(RequestInputReadResult.Success(DefaultRequestJson)), CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.PlanAsync(
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);
        Assert.Equal(1, preflightService.CallCount);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Plan,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62"));
    }

    private static ReadIndexInfo CreateReadIndexInfo (
        bool used,
        bool hit,
        string? fallbackReason)
    {
        return new ReadIndexInfo(
            Used: used,
            Hit: hit,
            Source: ReadIndexInfoSource.Index,
            Freshness: used
                ? IndexFreshness.Probable
                : IndexFreshness.Probable,
            GeneratedAtUtc: used
                ? DateTimeOffset.Parse("2026-03-06T00:00:00+00:00")
                : null,
            FallbackReason: fallbackReason);
    }

    private sealed class StubPlanService : IPlanService
    {
        private readonly Func<PlanCommandInput, CancellationToken, ValueTask<PlanServiceResult>> handler;

        public StubPlanService (
            Func<PlanCommandInput, CancellationToken, ValueTask<PlanServiceResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public PlanCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<PlanServiceResult> ExecuteAsync (
            PlanCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }

    private sealed class StubPlanCommandPreflightService : IPlanCommandPreflightService
    {
        private readonly Func<string?, string, ReadIndexMode?, CancellationToken, ValueTask<PlanCommandPreflightResult>> handler;

        public StubPlanCommandPreflightService (
            Func<string?, string, ReadIndexMode?, CancellationToken, ValueTask<PlanCommandPreflightResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public int CallCount { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<PlanCommandPreflightResult> PrepareAsync (
            string? projectPath,
            string requestJson,
            ReadIndexMode? readIndexMode,
            CancellationToken cancellationToken = default)
        {
            CapturedCancellationToken = cancellationToken;
            CallCount++;
            return handler(projectPath, requestJson, readIndexMode, cancellationToken);
        }
    }

    private sealed class StubRequestInputReader : IRequestInputReader
    {
        private readonly RequestInputReadResult result;

        public StubRequestInputReader (RequestInputReadResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public ValueTask<RequestInputReadResult> ReadAsync (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }
}
