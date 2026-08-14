using System.Text.Json;
using Json.Schema;
using MackySoft.Ucli.Application.Features.Eval;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;
using MackySoft.Ucli.Hosting.Cli.Schemas;
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
        AssertMatchesPublishedErrorSchema(payload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenPlanFails_ReportsTheClosedPlanFailurePayloadWithoutCallEvidence ()
    {
        var project = new UnityProjectIdentity(
            Path.GetFullPath("UnityProject"),
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
        AssertMatchesPublishedErrorSchema(payload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenCallFailsBeforeEntry_PreservesPlanAndPartialEvidenceWithoutReadPostcondition ()
    {
        var service = new RecordingEvalService((id, _, _) => ValueTask.FromResult(CreatePreEntryCallFailureServiceResult(id)));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        using var outputJson = JsonDocument.Parse(result.StdOut);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "detailed")
            .HasString("phase", "call")
            .HasString("applicationState", TextVocabulary.GetText(ExecutionApplicationState.NotApplied));
        Assert.True(payload.TryGetProperty("plan", out _));
        Assert.True(payload.TryGetProperty("eval", out _));
        Assert.False(payload.TryGetProperty("readPostcondition", out _));
        AssertMatchesPublishedErrorSchema(payload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Eval_WhenCallFailsAfterEntry_PreservesPlanPartialEvidenceAndReadPostcondition ()
    {
        var service = new RecordingEvalService((id, _, _) => ValueTask.FromResult(CreatePostEntryCallFailureServiceResult(id)));
        var sourceReader = new RecordingEvalSourceInputReader((_, _, _) => ValueTask.FromResult(EvalSourceInputReadResult.Success(EvalSource)));
        var command = new EvalCommand(service, sourceReader, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.EvalAsync(
            source: EvalSource,
            cancellationToken: CancellationToken.None));

        using var outputJson = JsonDocument.Parse(result.StdOut);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "detailed")
            .HasString("phase", "call")
            .HasString("applicationState", TextVocabulary.GetText(ExecutionApplicationState.Indeterminate));
        Assert.True(payload.TryGetProperty("plan", out _));
        Assert.True(payload.TryGetProperty("eval", out _));
        Assert.True(payload.TryGetProperty("readPostcondition", out _));
        AssertMatchesPublishedErrorSchema(payload);
    }

    private static void AssertMatchesPublishedErrorSchema (JsonElement payload)
    {
        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(
            schemaSet.Find("cli-output.payload.eval.error"));
        var schema = global::Json.Schema.JsonSchema.Build(
            artifact.Document,
            new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry
                {
                    Fetch = null!,
                },
            });
        var evaluation = schema.Evaluate(payload);
        Assert.True(
            evaluation.IsValid,
            "Published eval.error schema rejected a runtime failure payload:"
            + Environment.NewLine
            + JsonSerializer.Serialize(evaluation));
    }
}
