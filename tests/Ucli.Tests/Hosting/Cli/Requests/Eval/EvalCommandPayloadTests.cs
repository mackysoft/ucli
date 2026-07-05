using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.EvalCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class EvalCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WithSuccessResult_WritesEvalPayloadWithoutPlanRuntimeFields ()
    {
        var service = new RecordingCallService((_, _) => ValueTask.FromResult(CreateSuccessfulServiceResult()));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        EvalCommandAssert.SucceededWithPayload(
            result,
            expectedRequestId: RequestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenCallServiceRejectsDangerousOperation_WritesEvalFailure ()
    {
        var service = new RecordingCallService((_, _) => ValueTask.FromResult(CreateDangerousOperationRejectedResult()));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Eval);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("errors", 0, error => error
                .HasString("code", OperationAuthorizationErrorCodes.OperationNotAllowed.Value)
                .HasString("opId", "eval"));
    }
}
