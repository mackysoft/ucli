using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Call.Preflight;
using MackySoft.Ucli.Hosting.Cli.Requests.Input;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class CallCommandTests
{
    private const string DefaultRequestJson = """{"steps":[]}""";

    private const string ContractViolationMessage = "Operation result violated declared assurance facts.";

    [Fact]
    [Trait("Size", "Small")]
    public async Task Call_UsesCallServiceAndWritesCommandResult ()
    {
        var service = new StubCallService((input, _) => ValueTask.FromResult(CallServiceResult.Success(
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults:
                [
                    new OperationExecutionOperationResult(
                        OpId: "step-1",
                        Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                        Phase: IpcExecuteOperationPhaseNames.Call,
                        Applied: true,
                        Changed: false,
                        Touched: []),
                ],
                Plan: new CallPlanOutput(
                    RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    Project: ProjectIdentityInfoTestFactory.Create(),
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
                    PlanToken: "plan-token-1"),
                ReadPostcondition: null),
            "uCLI call completed.")));
        var preflightService = new StubCallCommandPreflightService((_, _, _) => throw new InvalidOperationException("Preflight should not be called."));
        var command = new CallCommand(service, preflightService, new StubRequestInputReader(RequestInputReadResult.Success(DefaultRequestJson)), CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.CallAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            planToken: "user-token",
            withPlan: true,
            allowDangerous: true,
            allowPlayMode: true,
            failFast: true,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        Assert.NotNull(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", service.CapturedInput!.ProjectPath);
        Assert.Equal(UnityExecutionMode.Oneshot, service.CapturedInput.Mode);
        Assert.Equal(1234, service.CapturedInput.TimeoutMilliseconds);
        Assert.Equal("user-token", service.CapturedInput.PlanToken);
        Assert.True(service.CapturedInput.WithPlan);
        Assert.True(service.CapturedInput.AllowDangerous);
        Assert.True(service.CapturedInput.AllowPlayMode);
        Assert.True(service.CapturedInput.FailFast);
        Assert.Equal(DefaultRequestJson, service.CapturedInput.RequestJson);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                .HasProperty("plan", plan => plan
                    .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")));
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("call", "success.json"),
            standardOutput,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Call_WhenPostReadSourceExists_WritesTopLevelPayload ()
    {
        var service = new StubCallService((_, _) => ValueTask.FromResult(CallServiceResult.Success(
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults:
                [
                    new OperationExecutionOperationResult(
                        OpId: "step-1",
                        Op: "edit",
                        Phase: IpcExecuteOperationPhaseNames.Call,
                        Applied: true,
                        Changed: true,
                        Touched: []),
                ],
                Plan: null,
                ReadPostcondition: null,
                PostReadSource: CreateEditPostReadSource()),
            "uCLI call completed.")));
        var preflightService = new StubCallCommandPreflightService((_, _, _) => throw new InvalidOperationException("Preflight should not be called."));
        var command = new CallCommand(service, preflightService, new StubRequestInputReader(RequestInputReadResult.Success(DefaultRequestJson)), CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.CallAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("postReadSource", postReadSource => postReadSource
                .HasInt32("schemaVersion", 1)
                .HasArrayLength("steps", 1)
                .HasProperty("steps", 0, step => step
                    .HasString("opId", "step-1")
                    .HasString("sourceKind", IpcExecutePostReadSourceKindNames.Edit)
                    .HasBoolean("playModeMutation", false)
                    .HasString("commit", IpcExecutePostReadCommitNames.Context)
                    .HasBoolean("persistenceExpected", true)
                    .HasString("expectedPostState", IpcExecuteExpectedPostStateNames.Deterministic)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Call_WhenContractViolationExists_MatchesGolden ()
    {
        var contractViolations = new[]
        {
            CreateContractViolation(IpcExecuteApplicationStateNames.Applied),
        };
        var planContractViolations = new[]
        {
            CreateContractViolation(IpcExecuteApplicationStateNames.Indeterminate),
        };
        var service = new StubCallService((_, _) => ValueTask.FromResult(CallServiceResult.Failure(
            ContractViolationMessage,
            [
                ApplicationFailure.FromCode(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    ContractViolationMessage,
                    "step-1"),
            ],
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults:
                [
                    CreateViolationOperationResult(IpcExecuteOperationPhaseNames.Call, applied: true),
                ],
                Plan: new CallPlanOutput(
                    RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    Project: ProjectIdentityInfoTestFactory.Create(),
                    OpResults:
                    [
                        CreateViolationOperationResult(IpcExecuteOperationPhaseNames.Plan, applied: false),
                    ],
                    PlanToken: "plan-token-1")
                {
                    ContractViolations = planContractViolations,
                },
                ReadPostcondition: null)
            {
                ContractViolations = contractViolations,
            })));
        var preflightService = new StubCallCommandPreflightService((_, _, _) => throw new InvalidOperationException("Preflight should not be called."));
        var command = new CallCommand(service, preflightService, new StubRequestInputReader(RequestInputReadResult.Success(DefaultRequestJson)), CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.CallAsync(
            projectPath: "/repo/UnityProject",
            withPlan: true,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, exitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("call", "contract-violation.json"),
            standardOutput,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Call_WhenModeIsInvalid_UsesFeatureFailurePathWithoutExecutingCall ()
    {
        var service = new StubCallService((_, _) => throw new InvalidOperationException("Execute should not be called."));
        var preflightService = new StubCallCommandPreflightService((_, _, _) => ValueTask.FromResult(CallCommandPreflightResult.Success(
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults: [],
                Plan: null,
                ReadPostcondition: null))));
        var command = new CallCommand(service, preflightService, new StubRequestInputReader(RequestInputReadResult.Success(DefaultRequestJson)), CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.CallAsync(
            mode: "unsupported",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);
        Assert.Equal(1, preflightService.CallCount);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Call,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62"));
    }

    private sealed class StubCallService : ICallService
    {
        private readonly Func<CallCommandInput, CancellationToken, ValueTask<CallServiceResult>> handler;

        public StubCallService (
            Func<CallCommandInput, CancellationToken, ValueTask<CallServiceResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public CallCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<CallServiceResult> ExecuteAsync (
            CallCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }

    private static OperationExecutionOperationResult CreateViolationOperationResult (
        string phase,
        bool applied)
    {
        return new OperationExecutionOperationResult(
            OpId: "step-1",
            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
            Phase: phase,
            Applied: applied,
            Changed: true,
            Touched:
            [
                new OperationExecutionTouchedResource(
                    Kind: UcliTouchedResourceKindNames.Asset,
                    Path: "Assets/Example.txt",
                    Guid: null),
            ]);
    }

    private static OperationExecutionContractViolation CreateContractViolation (string applicationState)
    {
        return new OperationExecutionContractViolation(
            OpId: "step-1",
            Operation: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
            ExpectedFact: "assurance.mayDirty=false",
            ObservedResult: "opResults[].changed=true",
            ApplicationState: applicationState);
    }

    private static OperationExecutionPostReadSource CreateEditPostReadSource ()
    {
        return new OperationExecutionPostReadSource(
            IpcExecutePostReadSource.CurrentSchemaVersion,
            [
                new OperationExecutionPostReadSourceStep(
                    OpId: "step-1",
                    SourceKind: IpcExecutePostReadSourceKindNames.Edit,
                    PlayModeMutation: false,
                    Commit: IpcExecutePostReadCommitNames.Context,
                    PersistenceExpected: true,
                    ExpectedPostState: IpcExecuteExpectedPostStateNames.Deterministic),
            ]);
    }

    private sealed class StubCallCommandPreflightService : ICallCommandPreflightService
    {
        private readonly Func<string?, string, CancellationToken, ValueTask<CallCommandPreflightResult>> handler;

        public StubCallCommandPreflightService (
            Func<string?, string, CancellationToken, ValueTask<CallCommandPreflightResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public int CallCount { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<CallCommandPreflightResult> PrepareAsync (
            string? projectPath,
            string requestJson,
            CancellationToken cancellationToken = default)
        {
            CapturedCancellationToken = cancellationToken;
            CallCount++;
            return handler(projectPath, requestJson, cancellationToken);
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
