using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;
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
                .HasArrayLength("opResults", 1)
                .HasProperty("plan", plan => plan
                    .HasArrayLength("opResults", 1)
                    .HasString("planToken", "plan-token-1")));
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("eval", "success.json"),
            standardOutput,
            CliOutputGoldenFiles.NormalizeRequestIds());
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

    private static void AssertEvalRequestJson (string requestJson)
    {
        using var document = JsonDocument.Parse(requestJson);
        JsonAssert.For(document.RootElement)
            .HasProperty("steps", 0, step => step
                .HasString("kind", "op")
                .HasString("id", "eval")
                .HasString("op", UcliPrimitiveOperationNames.CsEval)
                .HasProperty("args", args => args
                    .HasString("source", EvalSource)));
    }

    private static CallServiceResult CreateSuccessfulServiceResult ()
    {
        return CallServiceResult.Success(
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults:
                [
                    CreateOperationResult(IpcExecuteOperationPhaseNames.Call, applied: true),
                ],
                Plan: new CallPlanOutput(
                    RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    Project: ProjectIdentityInfoTestFactory.Create(),
                    OpResults:
                    [
                        CreateOperationResult(IpcExecuteOperationPhaseNames.Plan, applied: false),
                    ],
                    PlanToken: "plan-token-1"),
                ReadPostcondition: null),
            "uCLI call completed.");
    }

    private static OperationExecutionOperationResult CreateOperationResult (
        string phase,
        bool applied)
    {
        return new OperationExecutionOperationResult(
            OpId: "eval",
            Op: UcliPrimitiveOperationNames.CsEval,
            Phase: phase,
            Applied: applied,
            Changed: false,
            Touched: [])
        {
            Result = JsonSerializer.SerializeToElement(
                new
                {
                    sourceKind = CsEvalSourceKindValues.Snippet,
                    returnValue = new
                    {
                        kind = CsEvalReturnValueKindValues.Json,
                        value = new
                        {
                            ok = true,
                        },
                    },
                },
                IpcJsonSerializerOptions.Default),
        };
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
