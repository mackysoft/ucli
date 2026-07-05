using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.EvalCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class EvalCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenSourceInputFails_WritesEvalErrorWithoutExecutingCall ()
    {
        var service = new RecordingCallService((_, _) => throw new InvalidOperationException("Call should not execute."));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Failure(
            ExecutionError.InvalidArgument("Eval source was not provided."))));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            cancellationToken: CancellationToken.None));

        EvalCommandAssert.SourceInputFailureReturnedBeforeCallExecution(
            result,
            service);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenModeIsInvalid_WritesEvalErrorWithoutReadingSource ()
    {
        var service = new RecordingCallService((_, _) => throw new InvalidOperationException("Call should not execute."));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => throw new InvalidOperationException("Source should not be read."));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            mode: "unsupported",
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        EvalCommandAssert.InvalidModeRejectedBeforeSourceReadOrCallExecution(
            result,
            service,
            sourceReader);
    }
}
