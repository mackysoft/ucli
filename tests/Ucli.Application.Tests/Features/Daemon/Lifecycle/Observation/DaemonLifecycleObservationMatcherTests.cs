using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonLifecycleObservationMatcherTests
{
    private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid OtherEditorInstanceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenUserOwnedEditorInstanceIdsMatch_UsesEditorInstanceId ()
    {
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10),
            editorInstanceId: EditorInstanceId);
        var observation = CreateObservation(
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(20),
            editorInstanceId: EditorInstanceId);

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenUserOwnedEditorInstanceIdsDiffer_ReturnsFalseWithoutProcessStartFallback ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            processStartedAtUtc: startedAtUtc,
            editorInstanceId: EditorInstanceId);
        var observation = CreateObservation(
            processStartedAtUtc: startedAtUtc,
            editorInstanceId: OtherEditorInstanceId);

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenCliOwnedSessionHasNoEditorInstanceId_UsesProcessIdentity ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: startedAtUtc);
        var observation = CreateObservation(
            processStartedAtUtc: startedAtUtc.AddSeconds(1),
            editorInstanceId: EditorInstanceId);

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenCliOwnedProcessStartDiffersBeyondTolerance_ReturnsFalse ()
    {
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10));
        var observation = CreateObservation(
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(13),
            editorInstanceId: EditorInstanceId);

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenCliOwnedEditorInstanceIdsDiffer_UsesProcessIdentity ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: startedAtUtc,
            editorInstanceId: EditorInstanceId);
        var observation = CreateObservation(
            processStartedAtUtc: startedAtUtc.AddSeconds(1),
            editorInstanceId: OtherEditorInstanceId);

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSessionByEditorInstance_WhenSessionEditorInstanceIdIsMissing_ReturnsFalseWithoutProcessStartFallback ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: startedAtUtc);
        var observation = CreateObservation(
            processStartedAtUtc: startedAtUtc,
            editorInstanceId: EditorInstanceId);

        var result = DaemonLifecycleObservationMatcher.MatchesSessionByEditorInstance(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSessionByEditorInstance_WhenEditorInstanceIdsMatch_ReturnsTrue ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            editorMode: DaemonEditorMode.Gui,
            processStartedAtUtc: startedAtUtc,
            editorInstanceId: EditorInstanceId);
        var observation = CreateObservation(
            processStartedAtUtc: startedAtUtc.AddSeconds(20),
            editorInstanceId: EditorInstanceId);

        var result = DaemonLifecycleObservationMatcher.MatchesSessionByEditorInstance(observation, session);

        Assert.True(result);
    }

    private static DaemonLifecycleObservation CreateObservation (
        DateTimeOffset processStartedAtUtc,
        Guid editorInstanceId)
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
