using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

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
        var enterRequest = IpcPayloadCodec.SerializeToElement(new IpcPlayEnterRequest());
        var exitRequest = IpcPayloadCodec.SerializeToElement(new IpcPlayExitRequest());

        Assert.Equal(JsonValueKind.Object, statusRequest.ValueKind);
        Assert.Empty(statusRequest.EnumerateObject());
        Assert.Empty(enterRequest.EnumerateObject());
        Assert.False(exitRequest.TryGetProperty("timeoutMilliseconds", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayResponseContracts_SerializeWithCamelCaseFields ()
    {
        var before = CreateObservation(IpcPlayModeState.Stopped, IpcPlayModeTransition.None);
        var after = CreateObservation(IpcPlayModeState.Playing, IpcPlayModeTransition.None);
        var statusResponse = new IpcPlayStatusResponse(before);
        var transitionResponse = new IpcPlayTransitionResponse(
            new IpcPlayTransitionResult(
                Transition: IpcPlayTransitionCommand.Enter,
                Result: IpcPlayTransitionOutcome.Entered,
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
                    .HasString("lifecycleState", TextVocabulary.GetText(IpcEditorLifecycleState.Ready))
                    .HasString("compileState", TextVocabulary.GetText(IpcCompileState.Ready))
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
            .HasProperty("transition", transition => transition
                .HasString("transition", TextVocabulary.GetText(IpcPlayTransitionCommand.Enter))
                .HasString("result", TextVocabulary.GetText(IpcPlayTransitionOutcome.Entered))
                .HasProperty("before", beforeSnapshot => beforeSnapshot
                    .HasProperty("state", state => state
                        .HasProperty("playMode", playMode => playMode
                            .HasString("state", "stopped"))))
                .HasProperty("after", afterSnapshot => afterSnapshot
                    .HasProperty("state", state => state
                        .HasProperty("playMode", playMode => playMode
                            .HasString("state", "playing")))));

        Assert.False(transition.GetProperty("transition").TryGetProperty("applicationState", out _));

        var roundTrip = JsonSerializer.Deserialize<IpcPlayTransitionResponse>(
            transition.GetRawText(),
            IpcJsonSerializerOptions.Default);

        Assert.NotNull(roundTrip);
        Assert.Equal(IpcPlayTransitionCommand.Enter, roundTrip.Transition.Transition);
        Assert.Equal(IpcPlayTransitionOutcome.Entered, roundTrip.Transition.Result);
        Assert.Null(roundTrip.Transition.ApplicationState);
    }

    [Theory]
    [InlineData((IpcPlayTransitionCommand)0, IpcPlayTransitionOutcome.Entered)]
    [InlineData((IpcPlayTransitionCommand)100, IpcPlayTransitionOutcome.Entered)]
    [InlineData(IpcPlayTransitionCommand.Enter, (IpcPlayTransitionOutcome)0)]
    [InlineData(IpcPlayTransitionCommand.Enter, (IpcPlayTransitionOutcome)100)]
    [Trait("Size", "Small")]
    public void IpcPlayTransitionResult_RejectsUnmappedEnums (
        IpcPlayTransitionCommand transition,
        IpcPlayTransitionOutcome result)
    {
        var observation = CreateObservation(IpcPlayModeState.Playing, IpcPlayModeTransition.None);

        Assert.Throws<ArgumentOutOfRangeException>(() => new IpcPlayTransitionResult(
            Transition: transition,
            Result: result,
            Before: observation,
            After: observation,
            Observed: null,
            ApplicationState: null));
    }

    [Theory]
    [InlineData(IpcPlayTransitionCommand.Enter, IpcPlayTransitionOutcome.Exited)]
    [InlineData(IpcPlayTransitionCommand.Exit, IpcPlayTransitionOutcome.Entered)]
    [Trait("Size", "Small")]
    public void IpcPlayTransitionResult_RejectsOutcomeForAnotherCommand (
        IpcPlayTransitionCommand transition,
        IpcPlayTransitionOutcome result)
    {
        var observation = CreateObservation(IpcPlayModeState.Playing, IpcPlayModeTransition.None);

        Assert.Throws<ArgumentException>(() => new IpcPlayTransitionResult(
            Transition: transition,
            Result: result,
            Before: observation,
            After: observation,
            Observed: null,
            ApplicationState: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayTransitionResult_RejectsSuccessWithoutAfterSnapshot ()
    {
        var before = CreateObservation(IpcPlayModeState.Stopped, IpcPlayModeTransition.None);

        Assert.Throws<ArgumentNullException>(() => new IpcPlayTransitionResult(
            Transition: IpcPlayTransitionCommand.Enter,
            Result: IpcPlayTransitionOutcome.Entered,
            Before: before,
            After: null,
            Observed: null,
            ApplicationState: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayTransitionResult_RejectsFailureFieldsOnSuccess ()
    {
        var observation = CreateObservation(IpcPlayModeState.Playing, IpcPlayModeTransition.None);

        Assert.Throws<ArgumentException>(() => new IpcPlayTransitionResult(
            Transition: IpcPlayTransitionCommand.Enter,
            Result: IpcPlayTransitionOutcome.Entered,
            Before: observation,
            After: observation,
            Observed: observation,
            ApplicationState: IpcApplicationState.Applied));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayTransitionResult_RejectsIncompleteFailureEvidence ()
    {
        var observation = CreateObservation(IpcPlayModeState.Stopped, IpcPlayModeTransition.None);

        Assert.Throws<ArgumentNullException>(() => new IpcPlayTransitionResult(
            Transition: IpcPlayTransitionCommand.Enter,
            Result: IpcPlayTransitionOutcome.Blocked,
            Before: observation,
            After: null,
            Observed: null,
            ApplicationState: IpcApplicationState.NotApplied));
        Assert.Throws<ArgumentException>(() => new IpcPlayTransitionResult(
            Transition: IpcPlayTransitionCommand.Enter,
            Result: IpcPlayTransitionOutcome.Blocked,
            Before: observation,
            After: null,
            Observed: observation,
            ApplicationState: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IpcPlayTransitionResult(
            Transition: IpcPlayTransitionCommand.Enter,
            Result: IpcPlayTransitionOutcome.Blocked,
            Before: observation,
            After: null,
            Observed: observation,
            ApplicationState: (IpcApplicationState)0));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayTransitionResult_RejectsAfterSnapshotOnFailure ()
    {
        var observation = CreateObservation(IpcPlayModeState.Stopped, IpcPlayModeTransition.None);

        Assert.Throws<ArgumentException>(() => new IpcPlayTransitionResult(
            Transition: IpcPlayTransitionCommand.Enter,
            Result: IpcPlayTransitionOutcome.Blocked,
            Before: observation,
            After: observation,
            Observed: observation,
            ApplicationState: IpcApplicationState.NotApplied));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayTransitionResult_RequiresIndeterminateApplicationStateForTimeout ()
    {
        var observation = CreateObservation(IpcPlayModeState.Stopped, IpcPlayModeTransition.None);

        Assert.Throws<ArgumentOutOfRangeException>(() => new IpcPlayTransitionResult(
            Transition: IpcPlayTransitionCommand.Enter,
            Result: IpcPlayTransitionOutcome.Timeout,
            Before: observation,
            After: null,
            Observed: observation,
            ApplicationState: IpcApplicationState.Applied));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayTransitionResult_AllowsBlockedTransitionKnownToBeApplied ()
    {
        var observation = CreateObservation(IpcPlayModeState.Playing, IpcPlayModeTransition.None);

        var transition = new IpcPlayTransitionResult(
            Transition: IpcPlayTransitionCommand.Exit,
            Result: IpcPlayTransitionOutcome.Blocked,
            Before: observation,
            After: null,
            Observed: observation,
            ApplicationState: IpcApplicationState.Applied);

        Assert.Equal(IpcApplicationState.Applied, transition.ApplicationState);
    }

    private static IpcUnityEditorObservation CreateObservation (
        IpcPlayModeState playModeState,
        IpcPlayModeTransition transition)
    {
        return new IpcUnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: ProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Gui,
                lifecycleState: IpcEditorLifecycleState.Ready,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(
                    CompileGeneration: 12,
                    DomainReloadGeneration: 7,
                    AssetRefreshGeneration: 8,
                    PlayModeGeneration: 42),
                playMode: new IpcPlayModeSnapshot(
                    State: playModeState,
                    Transition: transition,
                    IsPlaying: playModeState == IpcPlayModeState.Playing,
                    IsPlayingOrWillChangePlaymode: playModeState == IpcPlayModeState.Playing
                        || transition == IpcPlayModeTransition.Entering)),
            observedAtUtc: DateTimeOffset.Parse("2026-05-21T00:00:00+00:00"),
            actionRequired: null,
            primaryDiagnostic: null);
    }
}
