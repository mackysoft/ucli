using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class PlayEnterCommandPayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Enter_WhenServiceSucceeds_EmitsEnterTransitionPayloadWithoutExecutionResults ()
    {
        var result = await ExecuteAsync(PlayEnterExecutionResult.Success(PlayEnterCommandTestData.CreateOutput()));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayEnter);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasProperty("project", project => project
                .HasString("projectPath", PlayCommandOutputTestData.ProjectPath)
                .HasString("projectFingerprint", PlayCommandOutputTestData.ProjectFingerprint)
                .HasString("unityVersion", PlayCommandOutputTestData.UnityVersion))
            .HasString("daemonStatus", "running")
            .HasString("editorMode", "gui")
            .HasString("lifecycleState", ContractLiteralCodec.ToValue(IpcEditorLifecycleState.PlayMode))
            .HasString("blockingReason", ContractLiteralCodec.ToValue(IpcEditorBlockingReason.PlayMode))
            .HasBoolean("canAcceptExecutionRequests", false)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "playing")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", true)
                .HasBoolean("isPlayingOrWillChangePlaymode", true)
                .HasString("generation", "3"))
            .HasProperty("transition", transition => transition
                .HasString("transition", IpcPlayTransitionCommandNames.Enter)
                .HasString("result", IpcPlayTransitionResultNames.Entered)
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
    public async Task Enter_WhenTransitionTimesOut_EmitsErrorEnvelopeWithObservedTransitionPayload ()
    {
        var output = PlayEnterCommandTestData.CreateOutput(IpcPlayTransitionResultNames.Timeout, includeAfter: false);
        var failure = ApplicationFailure.Timeout(
            "Unity Play Mode enter timed out after 1000 milliseconds.",
            PlayModeErrorCodes.PlayModeTransitionTimeout);

        var result = await ExecuteAsync(PlayEnterExecutionResult.Failure(failure, output));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayEnter,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PlayModeErrorCodes.PlayModeTransitionTimeout);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload").GetProperty("transition"))
            .HasString("result", IpcPlayTransitionResultNames.Timeout)
            .HasString("applicationState", IpcPlayApplicationStateNames.Indeterminate)
            .HasProperty("observed", _ => { });
        Assert.False(outputJson.RootElement.GetProperty("payload").GetProperty("transition").TryGetProperty("after", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Enter_WhenTransitionIsBlocked_EmitsObservedPayloadWithoutAfterOrExecutionResults ()
    {
        var output = PlayEnterCommandTestData.CreateOutput(
            IpcPlayTransitionResultNames.Blocked,
            includeAfter: false,
            applicationState: IpcPlayApplicationStateNames.NotApplied);
        var failure = ApplicationFailure.FromCode(
            PlayModeErrorCodes.PlayModeTransitionBlocked,
            "Unity Play Mode enter is blocked.");

        var result = await ExecuteAsync(PlayEnterExecutionResult.Failure(failure, output));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayEnter,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PlayModeErrorCodes.PlayModeTransitionBlocked);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload").GetProperty("transition"))
            .HasString("result", IpcPlayTransitionResultNames.Blocked)
            .HasString("applicationState", IpcPlayApplicationStateNames.NotApplied)
            .HasProperty("observed", _ => { });
        Assert.False(outputJson.RootElement.GetProperty("payload").GetProperty("transition").TryGetProperty("after", out _));
        Assert.False(outputJson.RootElement.GetProperty("payload").TryGetProperty("opResults", out _));
        Assert.DoesNotContain("\"touched\"", result.StdOut, StringComparison.Ordinal);
    }

    private static async Task<CommandExecutionResult> ExecuteAsync (PlayEnterExecutionResult executionResult)
    {
        var service = new RecordingPlayEnterService((_, _) => ValueTask.FromResult(executionResult));
        var command = new PlayEnterCommand(service, CommandResultTestWriter.Create());

        return await CommandResultCapture.ExecuteAsync(() => command.EnterAsync(
            timeout: "1000",
            cancellationToken: CancellationToken.None));
    }
}
