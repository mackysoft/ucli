using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Tests.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcPlayContractSerializationTests
{
    private const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly ProjectFingerprint ProjectFingerprint = new(ProjectFingerprintText);

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayRequestContracts_SerializeWithCamelCaseFields ()
    {
        var statusRequest = IpcPayloadCodec.SerializeToElement(new IpcPlayStatusRequest());
        var enterRequest = IpcPayloadCodec.SerializeToElement(new IpcPlayEnterRequest(
            LifecycleExecutionContractTestFactory.CreateStart(
                LifecycleExecutionKind.PlayEnter)));
        var exitRequest = IpcPayloadCodec.SerializeToElement(new IpcPlayExitRequest(
            LifecycleExecutionContractTestFactory.CreateStart(
                LifecycleExecutionKind.PlayExit)));

        Assert.Equal(JsonValueKind.Object, statusRequest.ValueKind);
        Assert.Empty(statusRequest.EnumerateObject());
        Assert.True(enterRequest.TryGetProperty("start", out _));
        Assert.True(exitRequest.TryGetProperty("start", out _));
        Assert.False(exitRequest.TryGetProperty("timeoutMilliseconds", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayResponseContracts_SerializeWithCamelCaseFields ()
    {
        var before = CreateObservation(UnityEditorPlayModeState.Stopped, UnityEditorPlayModeTransition.None);
        var after = CreateObservation(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None);
        var statusResponse = new IpcPlayStatusResponse(before);
        var transitionResponse = new IpcPlayTransitionResponse(
            LifecycleExecutionContractTestFactory.CreateReference(
                LifecycleExecutionKind.PlayEnter,
                ExecutionLifecycle.Terminal,
                LifecycleExecutionState.Completed),
            new PlayLifecycleTransitionResult(
                Transition: PlayLifecycleTransitionCommand.Enter,
                Result: PlayLifecycleTransitionOutcome.Entered,
                Before: before,
                After: after,
                Observed: null,
                ApplicationState: null));

        var status = IpcPayloadCodec.SerializeToElement(statusResponse);
        var transition = IpcPayloadCodec.SerializeToElement(transitionResponse);

        JsonAssert.For(status)
            .HasProperty("snapshot", snapshot => snapshot
                .HasString("serverVersion", "0.5.0")
                .HasString("unityVersion", "6000.1.4f1")
                .HasString("projectFingerprint", ProjectFingerprintText)
                .HasString("observedAtUtc", "2026-05-21T00:00:00+00:00")
                .HasProperty("state", state => state
                    .HasString("editorMode", "gui")
                    .HasString("lifecycleState", TextVocabulary.GetText(UnityEditorLifecycleState.Ready))
                    .HasString("compileState", TextVocabulary.GetText(UnityEditorCompileState.Ready))
                    .HasProperty("generations", generations => generations
                        .HasInt32("compileGeneration", 12)
                        .HasInt32("domainReloadGeneration", 7)
                        .HasInt32("assetRefreshGeneration", 8)
                        .HasInt32("playModeGeneration", 42))
                    .HasProperty("playMode", playMode => playMode
                        .HasString("state", "stopped")
                        .HasString("transition", "none")
                        .HasBoolean("isPlaying", false)
                        .HasBoolean("isPlayingOrWillChangePlaymode", false))));

        JsonAssert.For(transition)
            .HasProperty("lifecycleExecutionRef", reference => reference
                .HasString("kind", "play.enter")
                .HasString("lifecycle", "terminal"))
            .HasProperty("result", transitionResult => transitionResult
                .HasString("transition", TextVocabulary.GetText(PlayLifecycleTransitionCommand.Enter))
                .HasString("result", TextVocabulary.GetText(PlayLifecycleTransitionOutcome.Entered))
                .HasProperty("before", beforeSnapshot => beforeSnapshot
                    .HasProperty("state", state => state
                        .HasProperty("playMode", playMode => playMode
                            .HasString("state", "stopped"))))
                .HasProperty("after", afterSnapshot => afterSnapshot
                    .HasProperty("state", state => state
                        .HasProperty("playMode", playMode => playMode
                            .HasString("state", "playing")))));

        Assert.False(transition.GetProperty("result").TryGetProperty("applicationState", out _));

        var roundTrip = JsonSerializer.Deserialize<IpcPlayTransitionResponse>(
            transition.GetRawText(),
            IpcJsonSerializerOptions.Default);

        Assert.NotNull(roundTrip);
        Assert.Equal(PlayLifecycleTransitionCommand.Enter, roundTrip.Result.Transition);
        Assert.Equal(PlayLifecycleTransitionOutcome.Entered, roundTrip.Result.Result);
        Assert.Null(roundTrip.Result.ApplicationState);
    }

    [Theory]
    [InlineData((PlayLifecycleTransitionCommand)0, PlayLifecycleTransitionOutcome.Entered)]
    [InlineData((PlayLifecycleTransitionCommand)100, PlayLifecycleTransitionOutcome.Entered)]
    [InlineData(PlayLifecycleTransitionCommand.Enter, (PlayLifecycleTransitionOutcome)0)]
    [InlineData(PlayLifecycleTransitionCommand.Enter, (PlayLifecycleTransitionOutcome)100)]
    [Trait("Size", "Small")]
    public void PlayLifecycleTransitionResult_RejectsUnmappedEnums (
        PlayLifecycleTransitionCommand transition,
        PlayLifecycleTransitionOutcome result)
    {
        var observation = CreateObservation(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None);

        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayLifecycleTransitionResult(
            Transition: transition,
            Result: result,
            Before: observation,
            After: observation,
            Observed: null,
            ApplicationState: null));
    }

    [Theory]
    [InlineData(PlayLifecycleTransitionCommand.Enter, PlayLifecycleTransitionOutcome.Exited)]
    [InlineData(PlayLifecycleTransitionCommand.Exit, PlayLifecycleTransitionOutcome.Entered)]
    [Trait("Size", "Small")]
    public void PlayLifecycleTransitionResult_RejectsOutcomeForAnotherCommand (
        PlayLifecycleTransitionCommand transition,
        PlayLifecycleTransitionOutcome result)
    {
        var observation = CreateObservation(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None);

        Assert.Throws<ArgumentException>(() => new PlayLifecycleTransitionResult(
            Transition: transition,
            Result: result,
            Before: observation,
            After: observation,
            Observed: null,
            ApplicationState: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayLifecycleTransitionResult_RejectsSuccessWithoutAfterSnapshot ()
    {
        var before = CreateObservation(UnityEditorPlayModeState.Stopped, UnityEditorPlayModeTransition.None);

        Assert.Throws<ArgumentNullException>(() => new PlayLifecycleTransitionResult(
            Transition: PlayLifecycleTransitionCommand.Enter,
            Result: PlayLifecycleTransitionOutcome.Entered,
            Before: before,
            After: null,
            Observed: null,
            ApplicationState: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayLifecycleTransitionResult_RejectsFailureFieldsOnSuccess ()
    {
        var observation = CreateObservation(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None);

        Assert.Throws<ArgumentException>(() => new PlayLifecycleTransitionResult(
            Transition: PlayLifecycleTransitionCommand.Enter,
            Result: PlayLifecycleTransitionOutcome.Entered,
            Before: observation,
            After: observation,
            Observed: observation,
            ApplicationState: ExecutionApplicationState.Applied));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayLifecycleTransitionResult_RejectsObservationFromAnotherProject ()
    {
        var before = CreateObservation(
            UnityEditorPlayModeState.Stopped,
            UnityEditorPlayModeTransition.None);
        var after = CreateObservation(
            UnityEditorPlayModeState.Playing,
            UnityEditorPlayModeTransition.None,
            new ProjectFingerprint(new string('f', 64)));

        var exception = Assert.Throws<ArgumentException>(() =>
            new PlayLifecycleTransitionResult(
                PlayLifecycleTransitionCommand.Enter,
                PlayLifecycleTransitionOutcome.Entered,
                before,
                after,
                Observed: null,
                ApplicationState: null));

        Assert.Equal("After", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayLifecycleTransitionResult_RejectsIncompleteFailureEvidence ()
    {
        var observation = CreateObservation(UnityEditorPlayModeState.Stopped, UnityEditorPlayModeTransition.None);

        Assert.Throws<ArgumentNullException>(() => new PlayLifecycleTransitionResult(
            Transition: PlayLifecycleTransitionCommand.Enter,
            Result: PlayLifecycleTransitionOutcome.Blocked,
            Before: observation,
            After: null,
            Observed: null,
            ApplicationState: ExecutionApplicationState.NotApplied));
        Assert.Throws<ArgumentException>(() => new PlayLifecycleTransitionResult(
            Transition: PlayLifecycleTransitionCommand.Enter,
            Result: PlayLifecycleTransitionOutcome.Blocked,
            Before: observation,
            After: null,
            Observed: observation,
            ApplicationState: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayLifecycleTransitionResult(
            Transition: PlayLifecycleTransitionCommand.Enter,
            Result: PlayLifecycleTransitionOutcome.Blocked,
            Before: observation,
            After: null,
            Observed: observation,
            ApplicationState: (ExecutionApplicationState)0));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayLifecycleTransitionResult_RejectsAfterSnapshotOnFailure ()
    {
        var observation = CreateObservation(UnityEditorPlayModeState.Stopped, UnityEditorPlayModeTransition.None);

        Assert.Throws<ArgumentException>(() => new PlayLifecycleTransitionResult(
            Transition: PlayLifecycleTransitionCommand.Enter,
            Result: PlayLifecycleTransitionOutcome.Blocked,
            Before: observation,
            After: observation,
            Observed: observation,
            ApplicationState: ExecutionApplicationState.NotApplied));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayLifecycleTransitionResult_RequiresIndeterminateApplicationStateForTimeout ()
    {
        var observation = CreateObservation(UnityEditorPlayModeState.Stopped, UnityEditorPlayModeTransition.None);

        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayLifecycleTransitionResult(
            Transition: PlayLifecycleTransitionCommand.Enter,
            Result: PlayLifecycleTransitionOutcome.Timeout,
            Before: observation,
            After: null,
            Observed: observation,
            ApplicationState: ExecutionApplicationState.Applied));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PlayLifecycleTransitionResult_AllowsBlockedTransitionKnownToBeApplied ()
    {
        var observation = CreateObservation(UnityEditorPlayModeState.Playing, UnityEditorPlayModeTransition.None);

        var transition = new PlayLifecycleTransitionResult(
            Transition: PlayLifecycleTransitionCommand.Exit,
            Result: PlayLifecycleTransitionOutcome.Blocked,
            Before: observation,
            After: null,
            Observed: observation,
            ApplicationState: ExecutionApplicationState.Applied);

        Assert.Equal(ExecutionApplicationState.Applied, transition.ApplicationState);
    }

    private static UnityEditorObservation CreateObservation (
        UnityEditorPlayModeState playModeState,
        UnityEditorPlayModeTransition transition,
        ProjectFingerprint? projectFingerprint = null)
    {
        return new UnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: projectFingerprint ?? ProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: UnityEditorMode.Gui,
                lifecycleState: UnityEditorLifecycleState.Ready,
                compileState: UnityEditorCompileState.Ready,
                generations: new UnityEditorGenerationSnapshot(
                    CompileGeneration: 12,
                    DomainReloadGeneration: 7,
                    AssetRefreshGeneration: 8,
                    PlayModeGeneration: 42),
                playMode: new UnityEditorPlayModeSnapshot(
                    State: playModeState,
                    Transition: transition,
                    IsPlaying: playModeState == UnityEditorPlayModeState.Playing,
                    IsPlayingOrWillChangePlaymode: playModeState == UnityEditorPlayModeState.Playing
                        || transition == UnityEditorPlayModeTransition.Entering)),
            observedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00"),
            actionRequired: null,
            primaryDiagnostic: null);
    }
}
