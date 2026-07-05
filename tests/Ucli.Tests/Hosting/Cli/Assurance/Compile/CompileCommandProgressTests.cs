using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.CompileCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class CompileCommandProgressTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WithJsonFormat_WritesProgressEntryToStandardError ()
    {
        var service = new RecordingCompileService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                CompileProgressEventNames.Completed,
                CreateCompletedEntry(),
                cancellationToken);
            return CompileExecutionResult.Success(CreateOutput());
        });
        var command = new CompileCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.CompileAsync(
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Compile);
        var line = Assert.Single(result.StdErr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var entryJson = JsonDocument.Parse(line);
        AssertCompileStreamEnvelope(entryJson.RootElement, sequence: 1, CompileProgressEventNames.Completed);
        Assert.Equal("pass", entryJson.RootElement.GetProperty("payload").GetProperty("verdict").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WithDefaultFormat_WritesTextProgressToStandardError ()
    {
        var service = new RecordingCompileService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                CompileProgressEventNames.Completed,
                CreateCompletedEntry(),
                cancellationToken);
            return CompileExecutionResult.Success(CreateOutput());
        });
        var command = new CompileCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.CompileAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Compile);
        Assert.Equal(
            "compile runId=run-1 verdict=pass errorCount=0 warningCount=0 completed" + Environment.NewLine,
            result.StdErr);
    }

    private static void AssertCompileStreamEnvelope (
        JsonElement root,
        int sequence,
        string eventName)
    {
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(UcliCommandNames.Compile, root.GetProperty("command").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("streamId").GetString()));
        Assert.Equal(sequence, root.GetProperty("sequence").GetInt32());
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("timestamp").GetString(), out _));
        Assert.Equal(eventName, root.GetProperty("event").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
    }
}
