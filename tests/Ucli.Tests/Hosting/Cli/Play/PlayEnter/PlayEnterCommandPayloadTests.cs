using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
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
        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
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
            .HasString("lifecycleState", TextVocabulary.GetText(UnityEditorLifecycleState.PlayMode))
            .HasString("blockingReason", TextVocabulary.GetText(UnityEditorBlockingReason.PlayMode))
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
                .HasString("transition", TextVocabulary.GetText(PlayLifecycleTransitionCommand.Enter))
                .HasString("result", TextVocabulary.GetText(PlayLifecycleTransitionOutcome.Entered))
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
        var failureContext = PlayEnterCommandTestData.CreateFailureContext(
            PlayLifecycleTransitionOutcome.Timeout,
            ExecutionApplicationState.Indeterminate);
        var failure = ApplicationFailure.Timeout(
            "Unity Play Mode enter timed out after 1000 milliseconds.",
            PlayModeErrorCodes.PlayModeTransitionTimeout,
            instancePath: null,
            startupFailure: null);

        var result = await ExecuteAsync(PlayEnterExecutionResult.Failure(
            failure,
            failureContext));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayEnter,
            TextVocabulary.GetText(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PlayModeErrorCodes.PlayModeTransitionTimeout);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "transitionFailure")
            .HasString("applicationState", TextVocabulary.GetText(
                ExecutionApplicationState.Indeterminate));
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
        var transition = payload.GetProperty("transition");
        JsonAssert.For(transition)
            .HasString("transition", TextVocabulary.GetText(
                PlayLifecycleTransitionCommand.Enter))
            .HasString("result", TextVocabulary.GetText(PlayLifecycleTransitionOutcome.Timeout))
            .HasProperty("before", _ => { })
            .HasProperty("observed", _ => { });
        AssertPropertySet(
            transition,
            "transition",
            "result",
            "before",
            "observed");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Enter_WhenTransitionIsBlocked_EmitsObservedPayloadWithoutAfterOrExecutionResults ()
    {
        var failureContext = PlayEnterCommandTestData.CreateFailureContext(
            PlayLifecycleTransitionOutcome.Blocked,
            ExecutionApplicationState.NotApplied);
        var failure = ApplicationFailure.UnityIpcFailure(
            "Unity Play Mode enter is blocked.",
            PlayModeErrorCodes.PlayModeTransitionBlocked,
            instancePath: null,
            startupFailure: null);

        var result = await ExecuteAsync(PlayEnterExecutionResult.Failure(
            failure,
            failureContext));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.PlayEnter,
            TextVocabulary.GetText(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, PlayModeErrorCodes.PlayModeTransitionBlocked);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "transitionFailure")
            .HasString("applicationState", TextVocabulary.GetText(
                ExecutionApplicationState.NotApplied));
        JsonAssert.For(payload.GetProperty("transition"))
            .HasString("result", TextVocabulary.GetText(PlayLifecycleTransitionOutcome.Blocked))
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
    public async Task Enter_WhenDeadlineWinsAfterSuccess_EmitsTerminalFailureWithSuccessEvidence ()
    {
        var failure = ApplicationFailure.Timeout(
            "Play Mode enter reached its durable execution deadline.",
            LifecycleExecutionErrorCodes.DeadlineExceeded,
            instancePath: null,
            startupFailure: null);

        var result = await ExecuteAsync(PlayEnterExecutionResult.Failure(
            failure,
            PlayEnterCommandTestData.CreateTerminalFailureContext()));

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
                    PlayLifecycleTransitionCommand.Enter))
                .HasString("result", TextVocabulary.GetText(
                    PlayLifecycleTransitionOutcome.Entered))
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
    public async Task Enter_WhenStartContextCarriesResultlessTerminalFailure_EmitsOnlyStartPayload ()
    {
        var failure = ApplicationFailure.UnityIpcFailure(
            "The registered Unity Editor process exited.",
            LifecycleExecutionErrorCodes.UnityExited,
            instancePath: null,
            startupFailure: null);
        var failureContext = new PlayTransitionFailureContext(
            PlayCommandOutputTestData.CreateProject(),
            PlayCommandOutputTestData.CreateTerminalExecutionReference(
                LifecycleExecutionKind.PlayEnter,
                LifecycleExecutionState.Failed),
            ExecutionApplicationState.Indeterminate);

        var result = await ExecuteAsync(PlayEnterExecutionResult.Failure(
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
                    LifecycleExecutionKind.PlayEnter))
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

    [Fact]
    [Trait("Size", "Small")]
    public void Enter_WhenStartContextHasNoDurableStatusLocator_RejectsProjection ()
    {
        var failure = ApplicationFailure.Canceled(
            "Waiting for Unity Play Mode enter was canceled.");
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.PlayEnter);
        var failureContext = new PlayTransitionFailureContext(
            PlayCommandOutputTestData.CreateProject(),
            new ActiveExecutionRef(
                definition.ExecutionKind,
                Guid.Parse("8d816e63-b50a-4135-8f63-c89b48dc0d8a"),
                LifecycleExecutionDefinitionDigest.Calculate(definition),
                new ExecutionState(TextVocabulary.GetText(
                    LifecycleExecutionState.Registered)),
                statusLocator: null),
            ExecutionApplicationState.Unknown);

        Assert.Throws<ArgumentException>(() =>
        {
            _ = PlayEnterCommandResultFactory.Create(
                PlayEnterExecutionResult.Failure(
                    failure,
                    failureContext));
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Enter_WhenCallerWaitEndsAfterRegistration_EmitsOnlyReconnectableExecutionContext ()
    {
        var failure = ApplicationFailure.Canceled(
            "Waiting for Unity Play Mode enter was canceled.");

        var result = await ExecuteAsync(PlayEnterExecutionResult.Failure(
            failure,
            PlayEnterCommandTestData.CreateWaitFailureContext()));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("payloadKind", "start")
            .HasProperty("project", _ => { })
            .HasProperty("lifecycleExecutionRef", executionRef => executionRef
                .HasString("lifecycle", TextVocabulary.GetText(ExecutionLifecycle.Active))
                .HasString("kind", TextVocabulary.GetText(
                    LifecycleExecutionKind.PlayEnter)))
            .HasString("applicationState", TextVocabulary.GetText(
                ExecutionApplicationState.Unknown));
        Assert.Equal(
            new[]
            {
                "payloadKind",
                "project",
                "lifecycleExecutionRef",
                "applicationState",
            },
            payload.EnumerateObject().Select(property => property.Name));
    }

    private static void AssertPropertySet (
        System.Text.Json.JsonElement value,
        params string[] expectedPropertyNames)
    {
        Assert.Equal(
            expectedPropertyNames.Order(StringComparer.Ordinal),
            value.EnumerateObject()
                .Select(static property => property.Name)
                .Order(StringComparer.Ordinal));
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
