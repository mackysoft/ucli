using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonPingInfoClientAssert
{
    public static RecordingDaemonPingInfoClient.Invocation PingReadForSession (
        RecordingDaemonPingInfoClient pingInfoClient,
        ProjectContext expectedContext,
        TimeSpan expectedTimeout,
        string expectedSessionToken,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(pingInfoClient.Invocations);
        Assert.Equal(expectedContext.UnityProject, invocation.UnityProject);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        Assert.Equal(expectedSessionToken, invocation.SessionToken);
        Assert.True(invocation.ValidateProjectFingerprint);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        return invocation;
    }

    public static RecordingDaemonPingInfoClient.Invocation GuiSessionPingRead (
        RecordingDaemonPingInfoClient pingInfoClient,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSession expectedSession)
    {
        var invocation = Assert.Single(pingInfoClient.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedSession.SessionToken, invocation.SessionToken);
        Assert.False(invocation.ValidateProjectFingerprint);
        return invocation;
    }

    public static RecordingDaemonPingInfoClient.Invocation GuiSessionPingReadWithTimeoutAboveProbeCap (
        RecordingDaemonPingInfoClient pingInfoClient,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSession expectedSession)
    {
        var invocation = GuiSessionPingRead(pingInfoClient, expectedUnityProject, expectedSession);
        Assert.True(invocation.Timeout > DaemonTimeouts.ProbeAttemptTimeoutCap);
        return invocation;
    }

    public static void GuiSessionPingAttempted (
        RecordingDaemonPingInfoClient pingInfoClient,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSession expectedSession)
    {
        Assert.NotEmpty(pingInfoClient.Invocations);
        Assert.All(pingInfoClient.Invocations, invocation =>
        {
            Assert.Equal(expectedUnityProject, invocation.UnityProject);
            Assert.Equal(expectedSession.SessionToken, invocation.SessionToken);
            Assert.False(invocation.ValidateProjectFingerprint);
        });
    }

    public static void ExistingSessionPingAttempted (
        RecordingDaemonPingInfoClient pingInfoClient,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSession expectedSession,
        int expectedCount,
        CancellationToken expectedCancellationToken)
    {
        Assert.Equal(expectedCount, pingInfoClient.Invocations.Count);
        Assert.All(pingInfoClient.Invocations, invocation =>
        {
            Assert.Equal(expectedUnityProject, invocation.UnityProject);
            Assert.Equal(expectedSession.SessionToken, invocation.SessionToken);
            Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        });
    }

    public static void ReadinessProbeRetriedFor (
        RecordingDaemonPingInfoClient pingClient,
        ResolvedUnityProjectContext expectedUnityProject,
        CancellationToken expectedCancellationToken)
    {
        Assert.Collection(
            pingClient.Invocations,
            invocation => ReadinessProbeAttempt(invocation, expectedUnityProject, expectedCancellationToken),
            invocation => ReadinessProbeAttempt(invocation, expectedUnityProject, expectedCancellationToken));
    }

    public static void ReadinessProbeAttemptedOnceFor (
        RecordingDaemonPingInfoClient pingClient,
        ResolvedUnityProjectContext expectedUnityProject,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(pingClient.Invocations);
        ReadinessProbeAttempt(invocation, expectedUnityProject, expectedCancellationToken);
    }

    private static void ReadinessProbeAttempt (
        RecordingDaemonPingInfoClient.Invocation invocation,
        ResolvedUnityProjectContext expectedUnityProject,
        CancellationToken expectedCancellationToken)
    {
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
    }
}
