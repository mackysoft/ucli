namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class DaemonPingClientAssert
{
    public static RecordingDaemonPingClient.Invocation PingedOnceFor (
        RecordingDaemonPingClient pingClient,
        ResolvedUnityProjectContext expectedUnityProject,
        TimeSpan expectedTimeout)
    {
        var invocation = Assert.Single(pingClient.Invocations);
        Assert.Equal(expectedUnityProject.UnityProjectRoot, invocation.UnityProject.UnityProjectRoot);
        Assert.Equal(expectedUnityProject.RepositoryRoot, invocation.UnityProject.RepositoryRoot);
        Assert.Equal(expectedUnityProject.ProjectFingerprint, invocation.UnityProject.ProjectFingerprint);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        return invocation;
    }

    public static IReadOnlyList<RecordingDaemonPingClient.Invocation> PingedAtLeastOnce (RecordingDaemonPingClient pingClient)
    {
        Assert.NotEmpty(pingClient.Invocations);
        return pingClient.Invocations;
    }

    public static IReadOnlyList<RecordingDaemonPingClient.Invocation> PingAttemptsUseTimeoutAtMost (
        RecordingDaemonPingClient pingClient,
        TimeSpan maximumTimeout)
    {
        var invocations = PingedAtLeastOnce(pingClient);
        Assert.All(invocations, invocation => Assert.True(invocation.Timeout <= maximumTimeout));
        return invocations;
    }

    public static void StabilityVerificationPingsUsedCommandTimeoutBudget (
        RecordingDaemonPingClient pingClient,
        TimeSpan expectedTimeout,
        int expectedPingCount)
    {
        Assert.Equal(
            Enumerable.Repeat(expectedTimeout, expectedPingCount).ToArray(),
            pingClient.Timeouts);
    }

    public static void StabilityVerificationAttemptedBeforeRemainingTimeoutExhausted (RecordingDaemonPingClient pingClient)
    {
        Assert.NotEmpty(pingClient.Timeouts);
        if (pingClient.Timeouts.Count >= 2)
        {
            Assert.True(pingClient.Timeouts[^1] < pingClient.Timeouts[0]);
        }
    }

}
