using System.Text.Json;
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
                .HasString("projectFingerprint", PlayCommandOutputTestData.ProjectFingerprint.ToString())
                .HasString("unityVersion", PlayCommandOutputTestData.UnityVersion))
            .HasString("daemonStatus", "running")
            .HasString("editorMode", "gui")
            .HasString("lifecycleState", ContractLiteralCodec.ToValue(IpcEditorLifecycleState.Ready))
            .HasValueKind("blockingReason", JsonValueKind.Null)
            .HasProperty("generations", generations => generations
                .HasInt32("compileGeneration", 12)
                .HasInt32("domainReloadGeneration", 7)
                .HasInt32("assetRefreshGeneration", 0)
                .HasInt32("playModeGeneration", 3))
            .HasBoolean("canAcceptExecutionRequests", true)
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "stopped")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", false)
                .HasBoolean("isPlayingOrWillChangePlaymode", false))
            .HasProperty("transition", transition => transition
                .HasString("transition", ContractLiteralCodec.ToValue(IpcPlayTransitionCommand.Exit))
                .HasString("result", ContractLiteralCodec.ToValue(IpcPlayTransitionOutcome.Exited))
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
        var output = PlayExitCommandTestData.CreateOutput(IpcPlayTransitionOutcome.Timeout, includeAfter: false);
        var failure = ApplicationFailure.Timeout(
            "Unity Play Mode exit timed out after 1000 milliseconds.",
            PlayModeErrorCodes.PlayModeTransitionTimeout);

        var result = await ExecuteAsync(PlayExitExecutionResult.Failure(failure, output));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayExit,
            ContractLiteralCodec.ToValue(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PlayModeErrorCodes.PlayModeTransitionTimeout);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload").GetProperty("transition"))
            .HasString("result", ContractLiteralCodec.ToValue(IpcPlayTransitionOutcome.Timeout))
            .HasString("applicationState", ContractLiteralCodec.ToValue(IpcApplicationState.Indeterminate))
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
