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
            editorMode: "gui",
            ownerKind: "user",
            canShutdownProcess: false,
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10),
            editorInstanceId: EditorInstanceId);
        var observation = CreateObservation(processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(20));

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenUserOwnedEditorInstanceIdsDiffer_ReturnsFalseWithoutProcessStartFallback ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            editorMode: "gui",
            ownerKind: "user",
            canShutdownProcess: false,
            processStartedAtUtc: startedAtUtc,
            editorInstanceId: EditorInstanceId);
        var observation = CreateObservation(startedAtUtc, OtherEditorInstanceId);

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenCliOwnedSessionHasNoEditorInstanceId_UsesProcessIdentity ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            processStartedAtUtc: startedAtUtc);
        var observation = CreateObservation(
            startedAtUtc.AddSeconds(1),
            editorMode: "batchmode");

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenCliOwnedProcessStartDiffersBeyondTolerance_ReturnsFalse ()
    {
        var session = DaemonSessionTestFactory.Create(
            processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10));
        var observation = CreateObservation(
            DateTimeOffset.UnixEpoch.AddSeconds(13),
            editorMode: "batchmode");

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenCliOwnedEditorInstanceIdsDiffer_UsesProcessIdentity ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = DaemonSessionTestFactory.Create(
            processStartedAtUtc: startedAtUtc,
            editorInstanceId: OtherEditorInstanceId);
        var observation = CreateObservation(
            startedAtUtc,
            editorMode: "batchmode");

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenEditorInstanceIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateObservation(
            DateTimeOffset.UnixEpoch.AddSeconds(10),
            Guid.Empty));

        Assert.Equal("editorInstanceId", exception.ParamName);
    }

    private static DaemonLifecycleObservation CreateObservation (
        DateTimeOffset processStartedAtUtc,
        Guid? editorInstanceId = null,
        string editorMode = "gui")
    {
        return new DaemonLifecycleObservation(
            processId: 1234,
            processStartedAtUtc: processStartedAtUtc,
            editorMode: editorMode,
            lifecycleState: IpcEditorLifecycleStateCodec.Recovering,
            blockingReason: null,
            compileState: IpcCompileStateCodec.Ready,
            compileGeneration: "compile-generation-1",
            domainReloadGeneration: "domain-reload-generation-1",
            observedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(1),
            actionRequired: null,
            primaryDiagnostic: null,
            editorInstanceId: editorInstanceId ?? EditorInstanceId);
    }
}
