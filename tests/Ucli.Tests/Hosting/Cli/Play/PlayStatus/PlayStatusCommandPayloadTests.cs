using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayStatusCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenServiceSucceeds_EmitsFlatPlayStatusPayload ()
    {
        var service = new RecordingPlayStatusService((_, _) => ValueTask.FromResult(PlayStatusExecutionResult.Success(
            PlayStatusCommandTestData.CreateOutput())));
        var command = new PlayStatusCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.StatusAsync(
            timeout: "1000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayStatus);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("project", project => project
                .HasString("projectPath", PlayCommandOutputTestData.ProjectPath)
                .HasString("projectFingerprint", PlayCommandOutputTestData.ProjectFingerprint.ToString())
                .HasString("unityVersion", PlayCommandOutputTestData.UnityVersion))
            .HasString("daemonStatus", "running")
            .HasString("serverVersion", "0.5.0")
            .HasString("editorMode", "gui")
            .HasString("lifecycleState", "ready")
            .IsNull("blockingReason")
            .HasString("compileState", "ready")
            .HasString("compileGeneration", "12")
            .HasString("domainReloadGeneration", "7")
            .HasBoolean("canAcceptExecutionRequests", true)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "stopped")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", false)
                .HasBoolean("isPlayingOrWillChangePlaymode", false)
                .HasString("generation", "2"))
            .HasInt32("timeoutMilliseconds", 1000);
    }
}
