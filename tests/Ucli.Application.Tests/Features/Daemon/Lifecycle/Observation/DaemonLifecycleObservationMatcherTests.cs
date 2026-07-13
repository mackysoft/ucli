using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonLifecycleObservationMatcherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenBothEditorInstanceIdsArePresent_UsesEditorInstanceId ()
    {
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10)) with
        {
            EditorInstanceId = "editor-instance-1",
        };
        var observation = CreateObservation(
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(20),
            editorInstanceId: "editor-instance-1");

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenBothEditorInstanceIdsDiffer_ReturnsFalseWithoutProcessStartFallback ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: startedAtUtc) with
        {
            EditorInstanceId = "editor-instance-1",
        };
        var observation = CreateObservation(
            processStartedAtUtc: startedAtUtc,
            editorInstanceId: "editor-instance-2");

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenOnlyObservationHasEditorInstanceId_ReturnsFalseWithoutProcessStartFallback ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: startedAtUtc);
        var observation = CreateObservation(
            processStartedAtUtc: startedAtUtc.AddSeconds(1),
            editorInstanceId: "editor-instance-1");

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenOnlySessionHasEditorInstanceId_ReturnsFalseWithoutProcessStartFallback ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: startedAtUtc) with
        {
            EditorInstanceId = "editor-instance-1",
        };
        var observation = CreateObservation(processStartedAtUtc: startedAtUtc);

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenBothEditorInstanceIdsAreMissing_FallsBackToProcessStartTimeTolerance ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: startedAtUtc);
        var observation = CreateObservation(processStartedAtUtc: startedAtUtc.AddSeconds(1));

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenBothEditorInstanceIdsAreMissingAndProcessStartDiffersBeyondTolerance_ReturnsFalse ()
    {
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10));
        var observation = CreateObservation(processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(13));

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSessionByEditorInstance_WhenIdsMatchAndProcessStartDiffers_ReturnsTrue ()
    {
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10)) with
        {
            EditorInstanceId = "editor-instance-1",
        };
        var observation = CreateObservation(
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(20),
            editorInstanceId: "editor-instance-1");

        var result = DaemonLifecycleObservationMatcher.MatchesSessionByEditorInstance(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSessionByEditorInstance_WhenBothEditorInstanceIdsAreMissing_ReturnsFalseWithoutProcessStartFallback ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: startedAtUtc);
        var observation = CreateObservation(processStartedAtUtc: startedAtUtc);

        var result = DaemonLifecycleObservationMatcher.MatchesSessionByEditorInstance(observation, session);

        Assert.False(result);
    }

    private static DaemonLifecycleObservation CreateObservation (
        DateTimeOffset processStartedAtUtc,
        string? editorInstanceId = null)
    {
        return new DaemonLifecycleObservation(
            processId: 1234,
            processStartedAtUtc: processStartedAtUtc,
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Gui,
                lifecycleState: IpcEditorLifecycleState.Recovering,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(1, 1, 0, 0),
                playMode: new IpcPlayModeSnapshot(
                    IpcPlayModeState.Stopped,
                    IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(1),
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: editorInstanceId);
    }
}
