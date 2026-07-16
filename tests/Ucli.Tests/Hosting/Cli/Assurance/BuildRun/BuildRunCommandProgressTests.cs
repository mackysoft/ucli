using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCommandProgressTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithJsonFormat_WritesProgressEntryToStandardError ()
    {
        var service = new RecordingBuildService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                    BuildRunProgressEventNames.Completed,
                    BuildRunTestData.CreateCompletedEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            return BuildExecutionResult.Success(BuildRunTestData.CreateOutput());
        });
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            profilePath: "/repo/.ucli/build/player.json",
            format: "json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun);

        var line = Assert.Single(result.StdErr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var entryJson = JsonDocument.Parse(line);
        AssertBuildStreamEnvelope(entryJson.RootElement, sequence: 1, BuildRunProgressEventNames.Completed);
        Assert.Equal(BuildRunTestData.RunIdText, entryJson.RootElement.GetProperty("payload").GetProperty("runId").GetString());
        Assert.Equal("pass", entryJson.RootElement.GetProperty("payload").GetProperty("verdict").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Run_WithDefaultFormat_WritesTextProgressToStandardError ()
    {
        var service = new RecordingBuildService(async (_, progressSink, cancellationToken) =>
        {
            Assert.NotNull(progressSink);
            await progressSink!.OnEntryAsync(
                    BuildRunProgressEventNames.Started,
                    BuildRunTestData.CreateStartedEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            await progressSink.OnEntryAsync(
                    BuildRunProgressEventNames.Completed,
                    BuildRunTestData.CreateCompletedEntry(),
                    cancellationToken)
                .ConfigureAwait(false);
            return BuildExecutionResult.Success(BuildRunTestData.CreateOutput());
        });
        var command = new BuildRunCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.RunAsync(
            profilePath: "/repo/.ucli/build/player.json",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.BuildRun);
        Assert.Equal(
            $"build runId={BuildRunTestData.RunIdText} phase=started runnerKind=null runnerStatus=null verdict=null" + Environment.NewLine
                + $"build runId={BuildRunTestData.RunIdText} phase=completed runnerKind=buildPipeline runnerStatus=succeeded verdict=pass" + Environment.NewLine,
            result.StdErr);
    }

    private static void AssertBuildStreamEnvelope (
        JsonElement root,
        int sequence,
        string eventName)
    {
        Assert.Equal(1, root.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(UcliCommandNames.BuildRun, root.GetProperty("command").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("streamId").GetString()));
        Assert.Equal(sequence, root.GetProperty("sequence").GetInt32());
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("timestamp").GetString(), out _));
        Assert.Equal(eventName, root.GetProperty("event").GetString());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("payload").ValueKind);
    }
}
