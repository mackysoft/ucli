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
                .HasString("projectFingerprint", PlayCommandOutputTestData.ProjectFingerprint.ToString())
                .HasString("unityVersion", PlayCommandOutputTestData.UnityVersion))
            .HasString("daemonStatus", "running")
            .HasString("editorMode", "gui")
            .HasString("lifecycleState", TextVocabulary.GetText(IpcEditorLifecycleState.PlayMode))
            .HasString("blockingReason", TextVocabulary.GetText(IpcEditorBlockingReason.PlayMode))
            .HasProperty("generations", generations => generations
                .HasInt32("compileGeneration", 12)
                .HasInt32("domainReloadGeneration", 7)
                .HasInt32("assetRefreshGeneration", 0)
                .HasInt32("playModeGeneration", 3))
            .HasBoolean("canAcceptExecutionRequests", false)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "playing")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", true)
                .HasBoolean("isPlayingOrWillChangePlaymode", true))
            .HasProperty("transition", transition => transition
                .HasString("transition", TextVocabulary.GetText(IpcPlayTransitionCommand.Enter))
                .HasString("result", TextVocabulary.GetText(IpcPlayTransitionOutcome.Entered))
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
        var output = PlayEnterCommandTestData.CreateOutput(IpcPlayTransitionOutcome.Timeout, includeAfter: false);
        var failure = ApplicationFailure.Timeout(
            "Unity Play Mode enter timed out after 1000 milliseconds.",
            PlayModeErrorCodes.PlayModeTransitionTimeout);

        var result = await ExecuteAsync(PlayEnterExecutionResult.Failure(failure, output));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayEnter,
            TextVocabulary.GetText(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PlayModeErrorCodes.PlayModeTransitionTimeout);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload").GetProperty("transition"))
            .HasString("result", TextVocabulary.GetText(IpcPlayTransitionOutcome.Timeout))
            .HasString("applicationState", TextVocabulary.GetText(IpcApplicationState.Indeterminate))
            .HasProperty("observed", _ => { });
        Assert.False(outputJson.RootElement.GetProperty("payload").GetProperty("transition").TryGetProperty("after", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Enter_WhenTransitionIsBlocked_EmitsObservedPayloadWithoutAfterOrExecutionResults ()
    {
        var output = PlayEnterCommandTestData.CreateOutput(
            IpcPlayTransitionOutcome.Blocked,
            includeAfter: false,
            applicationState: IpcApplicationState.NotApplied);
        var failure = ApplicationFailure.FromCode(
            PlayModeErrorCodes.PlayModeTransitionBlocked,
            "Unity Play Mode enter is blocked.");

        var result = await ExecuteAsync(PlayEnterExecutionResult.Failure(failure, output));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayEnter,
            TextVocabulary.GetText(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PlayModeErrorCodes.PlayModeTransitionBlocked);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload").GetProperty("transition"))
            .HasString("result", TextVocabulary.GetText(IpcPlayTransitionOutcome.Blocked))
            .HasString("applicationState", TextVocabulary.GetText(IpcApplicationState.NotApplied))
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
