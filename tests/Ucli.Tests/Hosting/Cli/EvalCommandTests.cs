using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class EvalCommandTests
{
    private const string EvalSource = "context.DeclareNoTouchedResources(); return new { ok = true };";

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_UsesCallServiceWithGeneratedCsEvalRequest ()
    {
        var service = new StubCallService((_, _) => ValueTask.FromResult(CreateSuccessfulServiceResult()));
        var sourceReader = new StubEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.EvalAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            allowDangerous: true,
            allowPlayMode: true,
            failFast: true,
            source: EvalSource,
            file: null,
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(cancellationTokenSource.Token, sourceReader.CapturedCancellationToken);
        Assert.Equal(EvalSource, sourceReader.CapturedSource);
        Assert.Null(sourceReader.CapturedFile);
        Assert.NotNull(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", service.CapturedInput!.ProjectPath);
        Assert.Equal(UnityExecutionMode.Oneshot, service.CapturedInput.Mode);
        Assert.Equal(1234, service.CapturedInput.TimeoutMilliseconds);
        Assert.Null(service.CapturedInput.PlanToken);
        Assert.True(service.CapturedInput.WithPlan);
        Assert.True(service.CapturedInput.AllowDangerous);
        Assert.True(service.CapturedInput.AllowPlayMode);
        Assert.True(service.CapturedInput.FailFast);
        Assert.Equal(UcliCommandIds.Eval, service.CapturedInput.ExecutionOwnerCommand);
        AssertEvalRequestJson(service.CapturedInput.RequestJson);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "uCLI eval completed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                .HasValueKind("project", JsonValueKind.Object)
                .HasArrayLength("opResults", 1)
                .HasProperty("plan", plan => plan
                    .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                    .HasValueKind("project", JsonValueKind.Object)
                    .HasArrayLength("opResults", 1)
                    .HasString("planToken", "plan-token-1")));
        var planResult = outputJson.RootElement
            .GetProperty("payload")
            .GetProperty("plan")
            .GetProperty("opResults")[0]
            .GetProperty("result");
        Assert.False(planResult.TryGetProperty("returnValue", out _));
        Assert.False(planResult.TryGetProperty("logs", out _));
        Assert.False(planResult.TryGetProperty("durationMilliseconds", out _));
        Assert.False(planResult.TryGetProperty("touchedResources", out _));
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("eval", "success.json"),
            standardOutput,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenFileProvided_ReadsFileAndBuildsRequestFromFileSource ()
    {
        const string filePath = "eval-source.cs";
        const string fileSource = "return 2;";
        var service = new StubCallService((_, _) => ValueTask.FromResult(CreateSuccessfulServiceResult()));
        var sourceReader = new StubEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(fileSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var (exitCode, _) = await StandardOutputCapture.ExecuteAsync(() => command.EvalAsync(
            allowDangerous: true,
            source: null,
            file: filePath,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Null(sourceReader.CapturedSource);
        Assert.Equal(filePath, sourceReader.CapturedFile);
        Assert.NotNull(service.CapturedInput);
        AssertEvalRequestJson(service.CapturedInput!.RequestJson, fileSource);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenCallServiceRejectsDangerousOperation_WritesEvalFailure ()
    {
        var service = new StubCallService((_, _) => ValueTask.FromResult(CallServiceResult.Failure(
            "Static validation failed.",
            [
                ApplicationFailure.InvalidInput(
                    "Step 'eval' requires dangerous operation 'ucli.cs.eval'. Specify --allowDangerous to execute dangerous operations.",
                    OperationAuthorizationErrorCodes.OperationNotAllowed,
                    "eval"),
            ])));
        var sourceReader = new StubEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("errors", 0, error => error
                .HasString("code", OperationAuthorizationErrorCodes.OperationNotAllowed.Value)
                .HasString("opId", "eval"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenAllowDangerousIsFalse_PassesFalseToCallService ()
    {
        var service = new StubCallService((_, _) => ValueTask.FromResult(CreateSuccessfulServiceResult()));
        var sourceReader = new StubEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        _ = await StandardOutputCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        Assert.NotNull(service.CapturedInput);
        Assert.False(service.CapturedInput!.AllowDangerous);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenSourceInputFails_WritesEvalErrorWithoutExecutingCall ()
    {
        var service = new StubCallService((_, _) => throw new InvalidOperationException("Call should not execute."));
        var sourceReader = new StubEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Failure(
            ExecutionError.InvalidArgument("Eval source was not provided."))));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.EvalAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenModeIsInvalid_WritesEvalErrorWithoutReadingSource ()
    {
        var service = new StubCallService((_, _) => throw new InvalidOperationException("Call should not execute."));
        var sourceReader = new StubEvalSourceInputReader((_, _, _) => throw new InvalidOperationException("Source should not be read."));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.EvalAsync(
            mode: "unsupported",
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Equal(0, sourceReader.CallCount);
        Assert.Null(service.CapturedInput);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
    }

    private static void AssertEvalRequestJson (
        string requestJson,
        string expectedSource = EvalSource)
    {
        using var document = JsonDocument.Parse(requestJson);
        JsonAssert.For(document.RootElement)
            .HasProperty("steps", 0, step => step
                .HasString("kind", "op")
                .HasString("id", "eval")
                .HasString("op", UcliPrimitiveOperationNames.CsEval)
                .HasProperty("args", args => args
                    .HasString("source", expectedSource)));
    }

    private static CallServiceResult CreateSuccessfulServiceResult ()
    {
        return CallServiceResult.Success(
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults:
                [
                    CreateCallOperationResult(),
                ],
                Plan: new CallPlanOutput(
                    RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    Project: ProjectIdentityInfoTestFactory.Create(),
                    OpResults:
                    [
                        CreatePlanOperationResult(),
                    ],
                    PlanToken: "plan-token-1"),
                ReadPostcondition: null),
            "uCLI call completed.");
    }

    private static OperationExecutionOperationResult CreateCallOperationResult ()
    {
        return new OperationExecutionOperationResult(
            OpId: "eval",
            Op: UcliPrimitiveOperationNames.CsEval,
            Phase: IpcExecuteOperationPhaseNames.Call,
            Applied: true,
            Changed: false,
            Touched: [])
        {
            Result = IpcPayloadCodec.SerializeToElement(
                new CsEvalResult(
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    CsEvalSourceKindValues.Snippet,
                    "Snippet.Run",
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    CreateSuccessfulCompileResult(),
                    7,
                    [],
                    new CsEvalReturnValue(
                        CsEvalReturnValueKindValues.Json,
                        JsonSerializer.SerializeToElement(
                            new
                            {
                                ok = true,
                            },
                            IpcJsonSerializerOptions.Default)),
                    new CsEvalTouchedResources(
                        CsEvalTouchedResourceStateValues.None,
                        declared: null))),
        };
    }

    private static OperationExecutionOperationResult CreatePlanOperationResult ()
    {
        return new OperationExecutionOperationResult(
            OpId: "eval",
            Op: UcliPrimitiveOperationNames.CsEval,
            Phase: IpcExecuteOperationPhaseNames.Plan,
            Applied: false,
            Changed: false,
            Touched: [])
        {
            Result = IpcPayloadCodec.SerializeToElement(
                new CsEvalResult(
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    CsEvalSourceKindValues.Snippet,
                    "Snippet.Run",
                    "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    CreateSuccessfulCompileResult(),
                    durationMilliseconds: null,
                    logs: null,
                    returnValue: null,
                    touchedResources: null)),
        };
    }

    private static CsEvalCompileResult CreateSuccessfulCompileResult ()
    {
        return new CsEvalCompileResult(
            CsEvalCompileStatusValues.Succeeded,
            diagnostics: []);
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

        public ValueTask<CallServiceResult> ExecuteAsync (
            CallCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            return handler(input, cancellationToken);
        }
    }

    private sealed class StubEvalSourceInputReader : IEvalSourceInputReader
    {
        private readonly Func<string?, string?, CancellationToken, ValueTask<EvalSourceInputReadResult>> handler;

        public StubEvalSourceInputReader (
            Func<string?, string?, CancellationToken, ValueTask<EvalSourceInputReadResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public int CallCount { get; private set; }

        public string? CapturedSource { get; private set; }

        public string? CapturedFile { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<EvalSourceInputReadResult> ReadAsync (
            string? source,
            string? file,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CapturedSource = source;
            CapturedFile = file;
            CapturedCancellationToken = cancellationToken;
            return handler(source, file, cancellationToken);
        }
    }
}
