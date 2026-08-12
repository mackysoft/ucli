using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.EvalCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class EvalCommandGoldenOutputTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Eval_WithSuccessOutput_MatchesGolden ()
    {
        var service = new RecordingEvalService((id, _, _) => ValueTask.FromResult(CreateSuccessfulServiceResult(id)));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(source: EvalSource, cancellationToken: CancellationToken.None));

        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath("eval", "success.json"), result.StdOut, CliOutputGoldenFiles.NormalizeRequestIds());
    }
}
