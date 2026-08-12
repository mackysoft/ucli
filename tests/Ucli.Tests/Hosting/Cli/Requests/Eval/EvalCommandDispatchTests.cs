using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.EvalCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class EvalCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_MapsDedicatedEvalOptionsToServiceInput ()
    {
        var service = new RecordingEvalService((id, input, _) => ValueTask.FromResult(CreateSuccessfulServiceResult(id, input.SourceKind)));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            allowDangerous: true,
            allowPlayMode: true,
            source: EvalSource,
            sourceKind: "compilationUnit",
            cancellationToken: CancellationToken.None));

        EvalCommandAssert.HasDedicatedSuccessPayload(result, CsEvalSourceKind.CompilationUnit);
        EvalCommandAssert.HasDedicatedDispatch(service, EvalSource, CsEvalSourceKind.CompilationUnit, true, true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenSourceKindIsExplicitWhitespace_RejectsItBeforeServiceExecution ()
    {
        var service = new RecordingEvalService((_, _, _) => throw new Xunit.Sdk.XunitException("Eval service must not run for an invalid sourceKind."));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            sourceKind: " ",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(service.Invocations);
    }
}
