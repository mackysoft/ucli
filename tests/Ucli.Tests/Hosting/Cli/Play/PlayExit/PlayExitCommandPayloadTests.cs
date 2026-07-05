using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayExitCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Exit_WhenServiceSucceeds_EmitsExitTransitionPayloadWithoutExecutionResults ()
    {
        var result = await ExecuteAsync(PlayExitExecutionResult.Success(PlayExitCommandTestData.CreateOutput()));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayExit);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("project", project => project
                .HasString("projectPath", PlayCommandOutputTestData.ProjectPath)
                .HasString("projectFingerprint", PlayCommandOutputTestData.ProjectFingerprint)
                .HasString("unityVersion", PlayCommandOutputTestData.UnityVersion))
            .HasString("daemonStatus", "running")
            .HasString("editorMode", "gui")
            .HasString("lifecycleState", IpcEditorLifecycleStateCodec.Ready)
            .HasValueKind("blockingReason", JsonValueKind.Null)
            .HasBoolean("canAcceptExecutionRequests", true)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "stopped")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", false)
                .HasBoolean("isPlayingOrWillChangePlaymode", false)
                .HasString("generation", "3"))
            .HasProperty("transition", transition => transition
                .HasString("transition", IpcPlayTransitionCommandNames.Exit)
                .HasString("result", IpcPlayTransitionResultNames.Exited)
                .HasProperty("before", _ => { })
                .HasProperty("after", _ => { }))
            .HasInt32("timeoutMilliseconds", 1000);

        var transitionPayload = outputJson.RootElement.GetProperty("payload").GetProperty("transition");
        Assert.False(transitionPayload.TryGetProperty("observed", out _));
        Assert.False(transitionPayload.TryGetProperty("applicationState", out _));
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("opResults", out _));
        Assert.DoesNotContain("\"touched\"", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Exit_WhenTransitionTimesOut_EmitsErrorEnvelopeWithObservedTransitionPayload ()
    {
        var output = PlayExitCommandTestData.CreateOutput(IpcPlayTransitionResultNames.Timeout, includeAfter: false);
        var failure = ApplicationFailure.Timeout(
            "Unity Play Mode exit timed out after 1000 milliseconds.",
            PlayModeErrorCodes.PlayModeTransitionTimeout);

        var result = await ExecuteAsync(PlayExitExecutionResult.Failure(failure, output));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayExit,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PlayModeErrorCodes.PlayModeTransitionTimeout);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload").GetProperty("transition"))
            .HasString("result", IpcPlayTransitionResultNames.Timeout)
            .HasString("applicationState", IpcPlayApplicationStateNames.Indeterminate)
            .HasProperty("observed", _ => { });
        Assert.False(outputJson.RootElement.GetProperty("payload").GetProperty("transition").TryGetProperty("after", out _));
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("opResults", out _));
        Assert.DoesNotContain("\"touched\"", result.StdOut, StringComparison.Ordinal);
    }

    private static async Task<CommandExecutionResult> ExecuteAsync (PlayExitExecutionResult executionResult)
    {
        var service = new RecordingPlayExitService((_, _) => ValueTask.FromResult(executionResult));
        var command = new PlayExitCommand(service, CommandResultTestWriter.Create());

        return await CommandResultCapture.ExecuteAsync(() => command.ExitAsync(
            timeout: "1000",
            cancellationToken: CancellationToken.None));
    }
}
