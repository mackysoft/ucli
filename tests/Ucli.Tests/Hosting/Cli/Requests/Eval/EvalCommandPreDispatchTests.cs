using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class EvalCommandPreDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenSourceInputFails_DoesNotExecuteEvalService ()
    {
        var service = new RecordingEvalService((_, _, _) => throw new InvalidOperationException("Eval should not execute."));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Failure(ExecutionError.InvalidArgument("Eval source was not provided."))));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(service.Invocations);
    }
}
