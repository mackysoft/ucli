using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.VerifyCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class VerifyCommandProgressTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_WithJsonFormat_WritesProgressEntriesToStandardErrorAndFinalResultToStandardOutput ()
    {
        var service = new RecordingVerifyService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                    VerifyProgressEventNames.StepStarted,
                    CreateReadyStepProgressEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            await progressSink.OnEntryAsync(
                    VerifyProgressEventNames.StepCompleted,
                    CreateReadyStepProgressEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            return VerifyExecutionResult.Success(CreateOutput());
        });
        var command = new VerifyCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.VerifyAsync(
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Verify);
        var lines = result.StdErr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var startedEntry = JsonDocument.Parse(lines[0]);
        using var completedEntry = JsonDocument.Parse(lines[1]);
        AssertVerifyStreamEnvelope(startedEntry.RootElement, sequence: 1, VerifyProgressEventNames.StepStarted);
        AssertVerifyStreamEnvelope(completedEntry.RootElement, sequence: 2, VerifyProgressEventNames.StepCompleted);
        Assert.Equal(
            TextVocabulary.GetText(VerifyStepKind.Ready),
            startedEntry.RootElement.GetProperty("payload").GetProperty("kind").GetString());
        Assert.True(startedEntry.RootElement.GetProperty("payload").GetProperty("required").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_WithDefaultFormat_WritesTextProgressToStandardError ()
    {
        var service = new RecordingVerifyService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                    VerifyProgressEventNames.StepStarted,
                    CreateReadyStepProgressEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            await progressSink.OnEntryAsync(
                    VerifyProgressEventNames.StepCompleted,
                    CreateReadyStepProgressEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            await progressSink.OnEntryAsync(
                    VerifyProgressEventNames.StepSkipped,
                    CreateSkippedPostReadProgressEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            await progressSink.OnEntryAsync(
                    VerifyProgressEventNames.Diagnostic,
                    CreateDiagnosticEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            return VerifyExecutionResult.Success(CreateOutput());
        });
        var command = new VerifyCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.VerifyAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Verify);
        Assert.Equal(
            "verify ready required=true started" + Environment.NewLine
                + "verify ready required=true completed" + Environment.NewLine
                + "verify postRead required=false skipped" + Environment.NewLine
                + "verify diagnostic step=compile error VERIFY_STUB: stub diagnostic" + Environment.NewLine,
            result.StdErr);
    }

    private static void AssertVerifyStreamEnvelope (
        JsonElement root,
        int sequence,
        string eventName)
    {
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(UcliCommandNames.Verify, root.GetProperty("command").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("streamId").GetString()));
        Assert.Equal(sequence, root.GetProperty("sequence").GetInt32());
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("timestamp").GetString(), out _));
        Assert.Equal(eventName, root.GetProperty("event").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
    }
}
