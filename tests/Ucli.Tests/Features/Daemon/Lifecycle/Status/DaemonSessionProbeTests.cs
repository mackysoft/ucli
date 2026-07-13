using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonSessionProbeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenObservedSessionResponds_ReturnsSameSessionAndPing ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            "fingerprint-session-probe-current");
        var session = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "current-token");
        var pingResponse = IpcPingResponseTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint);
        var pingInfoClient = new RecordingDaemonPingInfoClient(pingResponse);
        var probe = new DaemonSessionProbe(
            new RecordingDaemonSessionStore(),
            pingInfoClient,
            new DaemonReachabilityClassifier());

        var result = await probe.ProbeAsync(
            unityProject,
            session,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(session, result.Session);
        Assert.Same(pingResponse, result.PingResponse);
        Assert.Null(result.SessionReadFailure);
        Assert.Equal(session, Assert.Single(pingInfoClient.Invocations).Session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenSessionTokenRotates_RereadsAndPingsReplacementOnce ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            "fingerprint-session-probe-rotation");
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "observed-token");
        var replacementSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "replacement-token",
            issuedAtUtc: observedSession.IssuedAtUtc.AddSeconds(1));
        var sessionStore = new RecordingDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(replacementSession));
        var replacementPing = IpcPingResponseTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint);
        var pingInfoClient = new RecordingDaemonPingInfoClient(
            new DaemonPingResponseException(
                "Session token rotated.",
                IpcSessionErrorCodes.SessionTokenInvalid),
            replacementPing);
        var probe = new DaemonSessionProbe(
            sessionStore,
            pingInfoClient,
            new DaemonReachabilityClassifier());

        var result = await probe.ProbeAsync(
            unityProject,
            observedSession,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(replacementSession, result.Session);
        Assert.Same(replacementPing, result.PingResponse);
        Assert.Single(sessionStore.ReadInvocations);
        Assert.Collection(
            pingInfoClient.Invocations,
            invocation => Assert.Equal(observedSession, invocation.Session),
            invocation => Assert.Equal(replacementSession, invocation.Session));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenReplacementSessionReadIsInvalid_ReturnsReadFailureWithoutSecondPing ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            "fingerprint-session-probe-invalid-replacement");
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "observed-token");
        var readFailure = DaemonSessionReadResultTestFactory.Invalid(
            artifactIdentity: DaemonSessionArtifactIdentity.Create("{ invalid"));
        var sessionStore = new RecordingDaemonSessionStore(readFailure);
        var pingInfoClient = new RecordingDaemonPingInfoClient(
            new DaemonPingResponseException(
                "Session token rotated.",
                IpcSessionErrorCodes.SessionTokenInvalid));
        var probe = new DaemonSessionProbe(
            sessionStore,
            pingInfoClient,
            new DaemonReachabilityClassifier());

        var result = await probe.ProbeAsync(
            unityProject,
            observedSession,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(readFailure, result.SessionReadFailure);
        Assert.Equal(observedSession, result.Session);
        Assert.Null(result.PingResponse);
        Assert.Null(result.ProbeFailure);
        Assert.Single(sessionStore.ReadInvocations);
        Assert.Single(pingInfoClient.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenReplacementProbeFails_ReturnsFailureWithReplacementSession ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            "fingerprint-session-probe-replacement-failure");
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "observed-token");
        var replacementSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "replacement-token",
            issuedAtUtc: observedSession.IssuedAtUtc.AddSeconds(1));
        var replacementFailure = new TimeoutException("Replacement did not respond.");
        var pingInfoClient = new RecordingDaemonPingInfoClient(
            new DaemonPingResponseException(
                "Session token rotated.",
                IpcSessionErrorCodes.SessionTokenInvalid),
            replacementFailure);
        var probe = new DaemonSessionProbe(
            new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(replacementSession)),
            pingInfoClient,
            new DaemonReachabilityClassifier());

        var result = await probe.ProbeAsync(
            unityProject,
            observedSession,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(replacementSession, result.Session);
        Assert.Same(replacementFailure, result.ProbeFailure);
        Assert.Null(result.PingResponse);
        Assert.Null(result.SessionReadFailure);
        Assert.Collection(
            pingInfoClient.Invocations,
            invocation => Assert.Equal(observedSession, invocation.Session),
            invocation => Assert.Equal(replacementSession, invocation.Session));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenRefreshedMetadataKeepsRejectedToken_DoesNotRetrySameToken ()
    {
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
            "fingerprint-session-probe-same-token");
        var observedSession = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "rejected-token");
        var refreshedMetadata = DaemonSessionTestFactory.Create(
            projectFingerprint: unityProject.ProjectFingerprint,
            sessionToken: "rejected-token",
            issuedAtUtc: observedSession.IssuedAtUtc.AddSeconds(1),
            endpointAddress: "updated-endpoint");
        var tokenRejection = new DaemonPingResponseException(
            "Session token was rejected.",
            IpcSessionErrorCodes.SessionTokenInvalid);
        var pingInfoClient = new RecordingDaemonPingInfoClient(tokenRejection);
        var probe = new DaemonSessionProbe(
            new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(refreshedMetadata)),
            pingInfoClient,
            new DaemonReachabilityClassifier());

        var result = await probe.ProbeAsync(
            unityProject,
            observedSession,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(1), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(observedSession, result.Session);
        Assert.Same(tokenRejection, result.ProbeFailure);
        Assert.Single(pingInfoClient.Invocations);
    }
}
