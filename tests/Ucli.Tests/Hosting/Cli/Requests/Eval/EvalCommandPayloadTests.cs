using System.Text.Json;
using MackySoft.Ucli.Application.Features.Eval;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.EvalCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class EvalCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WithSuccessResult_WritesDedicatedPayloadWithoutOperationResults ()
    {
        var service = new RecordingEvalService((id, _, _) => ValueTask.FromResult(CreateSuccessfulServiceResult(id)));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        EvalCommandAssert.HasDedicatedSuccessPayload(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenCallResponseIsIndeterminate_ReportsTheClosedCallFailurePayload ()
    {
        var service = new RecordingEvalService((id, _, _) => ValueTask.FromResult(CreateCallFailureServiceResult(id)));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = JsonDocument.Parse(result.StdOut);
        var root = outputJson.RootElement;
        CommandResultAssert.HasStandardEnvelope(root, UcliCommandNames.Eval, "error", (int)CliExitCode.ToolError);
        var payload = root.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "detailed")
            .HasString("phase", "call")
            .HasString("applicationState", TextVocabulary.GetText(ExecutionApplicationState.Indeterminate))
            .HasProperty("plan", plan => plan
                .HasString("eval.sourceKind", "snippet"));
        Assert.False(payload.TryGetProperty("opResults", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenPlanFails_ReportsTheClosedPlanFailurePayloadWithoutCallEvidence ()
    {
        var project = new UnityProjectIdentity(
            "/workspace/UnityProject",
            ProjectFingerprintTestFactory.Create("project-fingerprint"),
            "6000.1.4f1");
        var service = new RecordingEvalService((id, _, _) => ValueTask.FromResult(
            EvalServiceResult.Failure(id, project, ExecutionError.Timeout("eval.plan timed out."))));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        using var outputJson = JsonDocument.Parse(result.StdOut);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "detailed")
            .HasString("phase", "plan")
            .HasString("applicationState", TextVocabulary.GetText(ExecutionApplicationState.NotApplied));
        Assert.False(payload.TryGetProperty("plan", out _));
        Assert.False(payload.TryGetProperty("readPostcondition", out _));
    }
}
