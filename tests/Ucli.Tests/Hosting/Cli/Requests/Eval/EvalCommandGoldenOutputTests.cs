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
        var service = new RecordingCallService((_, _) => ValueTask.FromResult(CreateSuccessfulServiceResult()));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            allowDangerous: true,
            allowPlayMode: true,
            failFast: true,
            source: EvalSource,
            file: null,
            cancellationToken: CancellationToken.None));

        EvalCommandAssert.SucceededWithGolden(
            result,
            expectedRequestId: RequestId);
    }
}
