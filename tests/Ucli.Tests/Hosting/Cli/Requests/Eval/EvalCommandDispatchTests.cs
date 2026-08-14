using System.Text.Json;
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
    public async Task Eval_WhenSourceKindIsOmitted_DefaultsToSnippet ()
    {
        var service = new RecordingEvalService((id, input, _) => ValueTask.FromResult(CreateSuccessfulServiceResult(id, input.SourceKind)));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        EvalCommandAssert.HasDedicatedSuccessPayload(result, CsEvalSourceKind.Snippet);
        EvalCommandAssert.HasDedicatedDispatch(service, EvalSource, CsEvalSourceKind.Snippet, false, false);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenSourceKindIsCanonicalSnippet_DispatchesSnippet ()
    {
        var service = new RecordingEvalService((id, input, _) => ValueTask.FromResult(CreateSuccessfulServiceResult(id, input.SourceKind)));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            sourceKind: "snippet",
            cancellationToken: CancellationToken.None));

        EvalCommandAssert.HasDedicatedSuccessPayload(result, CsEvalSourceKind.Snippet);
        EvalCommandAssert.HasDedicatedDispatch(service, EvalSource, CsEvalSourceKind.Snippet, false, false);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("snippet ")]
    [InlineData(" compilationUnit")]
    [InlineData("Snippet")]
    [InlineData("CompilationUnit")]
    [Trait("Size", "Small")]
    public async Task Eval_WhenSourceKindIsNotCanonical_RejectsItBeforeServiceExecution (string sourceKind)
    {
        var service = new RecordingEvalService((_, _, _) => throw new Xunit.Sdk.XunitException("Eval service must not run for an invalid sourceKind."));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            sourceKind: sourceKind,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(service.Invocations);
        using var outputJson = JsonDocument.Parse(result.StdOut);
        var message = outputJson.RootElement.GetProperty("message").GetString();
        Assert.Contains("'snippet'", message, StringComparison.Ordinal);
        Assert.Contains("'compilationUnit'", message, StringComparison.Ordinal);
    }
}
