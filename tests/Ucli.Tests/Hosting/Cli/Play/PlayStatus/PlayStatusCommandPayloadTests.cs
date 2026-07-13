using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayStatusCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenServiceSucceeds_EmitsPlayStatusPayload ()
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
            .HasProperty("generations", generations => generations
                .HasInt32("compileGeneration", 12)
                .HasInt32("domainReloadGeneration", 7)
                .HasInt32("assetRefreshGeneration", 0)
                .HasInt32("playModeGeneration", 2))
            .HasBoolean("canAcceptExecutionRequests", true)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "stopped")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", false)
                .HasBoolean("isPlayingOrWillChangePlaymode", false))
            .HasInt32("timeoutMilliseconds", 1000);
    }
}
