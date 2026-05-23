using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonLifecycleObservationMatcherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenBothEditorInstanceIdsArePresent_UsesEditorInstanceId ()
    {
        var session = CreateSession(processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10)) with
        {
            EditorInstanceId = "editor-instance-1",
        };
        var observation = CreateObservation(processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(20)) with
        {
            EditorInstanceId = "editor-instance-1",
        };

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenBothEditorInstanceIdsDiffer_ReturnsFalseWithoutProcessStartFallback ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = CreateSession(processStartedAtUtc: startedAtUtc) with
        {
            EditorInstanceId = "editor-instance-1",
        };
        var observation = CreateObservation(processStartedAtUtc: startedAtUtc) with
        {
            EditorInstanceId = "editor-instance-2",
        };

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenOnlyObservationHasEditorInstanceId_ReturnsFalseWithoutProcessStartFallback ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = CreateSession(processStartedAtUtc: startedAtUtc);
        var observation = CreateObservation(processStartedAtUtc: startedAtUtc.AddSeconds(1)) with
        {
            EditorInstanceId = "editor-instance-1",
        };

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenOnlySessionHasEditorInstanceId_ReturnsFalseWithoutProcessStartFallback ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = CreateSession(processStartedAtUtc: startedAtUtc) with
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
        var session = CreateSession(processStartedAtUtc: startedAtUtc);
        var observation = CreateObservation(processStartedAtUtc: startedAtUtc.AddSeconds(1));

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSession_WhenBothEditorInstanceIdsAreMissingAndProcessStartDiffersBeyondTolerance_ReturnsFalse ()
    {
        var session = CreateSession(processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10));
        var observation = CreateObservation(processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(13));

        var result = DaemonLifecycleObservationMatcher.MatchesSession(observation, session);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSessionByEditorInstance_WhenIdsMatchAndProcessStartDiffers_ReturnsTrue ()
    {
        var session = CreateSession(processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(10)) with
        {
            EditorInstanceId = "editor-instance-1",
        };
        var observation = CreateObservation(processStartedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(20)) with
        {
            EditorInstanceId = "editor-instance-1",
        };

        var result = DaemonLifecycleObservationMatcher.MatchesSessionByEditorInstance(observation, session);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void MatchesSessionByEditorInstance_WhenBothEditorInstanceIdsAreMissing_ReturnsFalseWithoutProcessStartFallback ()
    {
        var startedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var session = CreateSession(processStartedAtUtc: startedAtUtc);
        var observation = CreateObservation(processStartedAtUtc: startedAtUtc);

        var result = DaemonLifecycleObservationMatcher.MatchesSessionByEditorInstance(observation, session);

        Assert.False(result);
    }

    private static DaemonSession CreateSession (DateTimeOffset processStartedAtUtc)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
            SessionToken: "token-1",
            ProjectFingerprint: "fingerprint-1",
            IssuedAtUtc: DateTimeOffset.UnixEpoch,
            EditorMode: DaemonEditorModeValues.Gui,
            OwnerKind: DaemonSessionOwnerKindValues.User,
            CanShutdownProcess: false,
            EndpointTransportKind: IpcTransportKindValues.NamedPipe,
            EndpointAddress: "ucli-daemon",
            ProcessId: 1234,
            ProcessStartedAtUtc: processStartedAtUtc,
            OwnerProcessId: 1234);
    }

    private static DaemonLifecycleObservation CreateObservation (DateTimeOffset processStartedAtUtc)
    {
        return new DaemonLifecycleObservation(
            ProcessId: 1234,
            ProcessStartedAtUtc: processStartedAtUtc,
            EditorMode: DaemonEditorModeValues.Gui,
            LifecycleState: IpcEditorLifecycleStateCodec.Recovering,
            BlockingReason: null,
            CompileState: IpcCompileStateCodec.Ready,
            CompileGeneration: "compile-generation-1",
            DomainReloadGeneration: "domain-reload-generation-1",
            ObservedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(1),
            ActionRequired: null,
            PrimaryDiagnostic: null);
    }
}
