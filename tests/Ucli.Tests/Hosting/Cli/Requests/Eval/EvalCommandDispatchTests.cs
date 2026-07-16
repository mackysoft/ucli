using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.EvalCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class EvalCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_MapsSnippetOptionsToCallServiceInputAndRequestJson ()
    {
        var service = new RecordingCallService((_, _) => ValueTask.FromResult(CreateSuccessfulServiceResult()));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            allowDangerous: true,
            allowPlayMode: true,
            failFast: true,
            source: EvalSource,
            file: null,
            cancellationToken: cancellationTokenSource.Token));

        EvalCommandAssert.SnippetRequestSucceededWithDispatch(
            result,
            service,
            sourceReader,
            cancellationTokenSource.Token,
            "/repo/UnityProject",
            UnityExecutionMode.Oneshot,
            expectedTimeoutMilliseconds: 1234,
            expectedSource: EvalSource);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenFileProvided_ReadsFileAndBuildsRequestFromFileSource ()
    {
        const string filePath = "eval-source.cs";
        const string fileSource = "return 2;";
        var service = new RecordingCallService((_, _) => ValueTask.FromResult(CreateSuccessfulServiceResult()));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(fileSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            allowDangerous: true,
            source: null,
            file: filePath,
            cancellationToken: CancellationToken.None));

        EvalCommandAssert.FileSourceRequestSucceeded(
            result,
            service,
            sourceReader,
            filePath,
            fileSource);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenAllowDangerousIsFalse_PassesFalseToCallService ()
    {
        var service = new RecordingCallService((_, _) => ValueTask.FromResult(CreateSuccessfulServiceResult()));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        EvalCommandAssert.DangerousExecutionDisallowedByDefault(
            result,
            service,
            sourceReader,
            EvalSource);
    }
}
