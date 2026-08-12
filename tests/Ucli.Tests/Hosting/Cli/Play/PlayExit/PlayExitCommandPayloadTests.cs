using System.Text.Json;
using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
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
        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
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
            .HasString("lifecycleState", TextVocabulary.GetText(UnityEditorLifecycleState.Ready))
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
                .HasString("transition", TextVocabulary.GetText(PlayLifecycleTransitionCommand.Exit))
                .HasString("result", TextVocabulary.GetText(PlayLifecycleTransitionOutcome.Exited))
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
        var failureContext = PlayExitCommandTestData.CreateFailureContext(
            PlayLifecycleTransitionOutcome.Timeout,
            ExecutionApplicationState.Indeterminate);
        var failure = ApplicationFailure.Timeout(
            "Unity Play Mode exit timed out after 1000 milliseconds.",
            PlayModeErrorCodes.PlayModeTransitionTimeout,
            instancePath: null,
            startupFailure: null);

        var result = await ExecuteAsync(PlayExitExecutionResult.Failure(
            failure,
            failureContext));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayExit,
            TextVocabulary.GetText(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PlayModeErrorCodes.PlayModeTransitionTimeout);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "transitionFailure")
            .HasString("applicationState", TextVocabulary.GetText(
                ExecutionApplicationState.Indeterminate));
        JsonAssert.For(payload.GetProperty("transition"))
            .HasString("result", TextVocabulary.GetText(PlayLifecycleTransitionOutcome.Timeout))
            .HasProperty("observed", _ => { });
        Assert.False(payload.GetProperty("transition").TryGetProperty("after", out _));
        Assert.False(payload.GetProperty("transition").TryGetProperty(
            "applicationState",
            out _));
        Assert.False(payload.TryGetProperty("opResults", out _));
        Assert.DoesNotContain("\"touched\"", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Exit_WhenTerminalRecordPublicationFails_RetainsSuccessEvidenceWithRecoveryReference ()
    {
        var failure = ApplicationFailure.UnityIpcFailure(
            "The Lifecycle Execution Terminal Record could not be published.",
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            instancePath: null,
            startupFailure: null);

        var result = await ExecuteAsync(PlayExitExecutionResult.Failure(
            failure,
            PlayExitCommandTestData.CreatePublicationFailureContext()));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "terminalPublicationFailure")
            .HasProperty("lifecycleExecutionRef", executionRef => executionRef
                .HasString("lifecycle", TextVocabulary.GetText(
                    ExecutionLifecycle.Recovery))
                .HasString("kind", TextVocabulary.GetText(
                    LifecycleExecutionKind.PlayExit)))
            .HasString("applicationState", TextVocabulary.GetText(
                ExecutionApplicationState.Applied))
            .HasProperty("transition", transition => transition
                .HasString("transition", TextVocabulary.GetText(
                    PlayLifecycleTransitionCommand.Exit))
                .HasString("result", TextVocabulary.GetText(
                    PlayLifecycleTransitionOutcome.Exited))
                .HasProperty("before", _ => { })
                .HasProperty("after", _ => { }));
        AssertPropertySet(
            payload,
            "payloadKind",
            "project",
            "lifecycleExecutionRef",
            "applicationState",
            "daemonStatus",
            "serverVersion",
            "editorMode",
            "lifecycleState",
            "blockingReason",
            "compileState",
            "generations",
            "canAcceptExecutionRequests",
            "observedAtUtc",
            "actionRequired",
            "primaryDiagnostic",
            "playMode",
            "transition",
            "timeoutMilliseconds");
        AssertPropertySet(
            payload.GetProperty("transition"),
            "transition",
            "result",
            "before",
            "after");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Exit_WhenDeadlineWinsAfterSuccess_EmitsTerminalFailureWithSuccessEvidence ()
    {
        var failure = ApplicationFailure.Timeout(
            "Play Mode exit reached its durable execution deadline.",
            LifecycleExecutionErrorCodes.DeadlineExceeded,
            instancePath: null,
            startupFailure: null);

        var result = await ExecuteAsync(PlayExitExecutionResult.Failure(
            failure,
            PlayExitCommandTestData.CreateTerminalFailureContext()));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson =
            JsonAssert.ParseMultilineObject(result.StdOut);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "terminalFailure")
            .HasProperty("lifecycleExecutionRef", executionRef => executionRef
                .HasString("lifecycle", TextVocabulary.GetText(
                    ExecutionLifecycle.Terminal))
                .HasString("state", TextVocabulary.GetText(
                    LifecycleExecutionState.Failed)))
            .HasString("applicationState", TextVocabulary.GetText(
                ExecutionApplicationState.Applied))
            .HasProperty("transition", transition => transition
                .HasString("transition", TextVocabulary.GetText(
                    PlayLifecycleTransitionCommand.Exit))
                .HasString("result", TextVocabulary.GetText(
                    PlayLifecycleTransitionOutcome.Exited))
                .HasProperty("before", _ => { })
                .HasProperty("after", _ => { }));
        AssertPropertySet(
            payload.GetProperty("transition"),
            "transition",
            "result",
            "before",
            "after");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Exit_WhenStartContextCarriesResultlessTerminalFailure_EmitsOnlyStartPayload ()
    {
        var failure = ApplicationFailure.UnityIpcFailure(
            "The registered Unity Editor process exited.",
            LifecycleExecutionErrorCodes.UnityExited,
            instancePath: null,
            startupFailure: null);
        var failureContext = new PlayTransitionFailureContext(
            PlayCommandOutputTestData.CreateProject(),
            PlayCommandOutputTestData.CreateTerminalExecutionReference(
                LifecycleExecutionKind.PlayExit,
                LifecycleExecutionState.Failed),
            ExecutionApplicationState.Indeterminate);

        var result = await ExecuteAsync(PlayExitExecutionResult.Failure(
            failure,
            failureContext));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson =
            JsonAssert.ParseMultilineObject(result.StdOut);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            LifecycleExecutionErrorCodes.UnityExited);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "start")
            .HasProperty("project", _ => { })
            .HasProperty("lifecycleExecutionRef", executionRef => executionRef
                .HasString("lifecycle", TextVocabulary.GetText(
                    ExecutionLifecycle.Terminal))
                .HasString("kind", TextVocabulary.GetText(
                    LifecycleExecutionKind.PlayExit))
                .HasString("state", TextVocabulary.GetText(
                    LifecycleExecutionState.Failed)))
            .HasString("applicationState", TextVocabulary.GetText(
                ExecutionApplicationState.Indeterminate));
        AssertPropertySet(
            payload,
            "payloadKind",
            "project",
            "lifecycleExecutionRef",
            "applicationState");
    }

    private static void AssertPropertySet (
        JsonElement value,
        params string[] expectedPropertyNames)
    {
        Assert.Equal(
            expectedPropertyNames.Order(StringComparer.Ordinal),
            value.EnumerateObject()
                .Select(static property => property.Name)
                .Order(StringComparer.Ordinal));
    }

    private static async Task<CommandExecutionResult> ExecuteAsync (PlayExitExecutionResult executionResult)
    {
        var service = new RecordingPlayExitService((_, _) => ValueTask.FromResult(executionResult));
        var command = new PlayExitCommand(service, CommandResultTestWriter.Create(), new RecordingLifecycleExecutionStartInvocationFactory());

        return await CommandResultCapture.ExecuteAsync(() => command.ExitAsync(
            timeout: "1000",
            cancellationToken: CancellationToken.None));
    }
}
