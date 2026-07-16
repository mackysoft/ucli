namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonGuiAttachInvocationAssert
{
    public static RecordingDaemonGuiSessionRegistrationAwaiter.Invocation EndpointWaitAttemptedFor (
        RecordingDaemonGuiSessionRegistrationAwaiter awaiter,
        ResolvedUnityProjectContext expectedUnityProject,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc,
        TimeSpan? expectedTimeout = null)
    {
        var invocation = Assert.Single(awaiter.Invocations);
        AssertEndpointWait(invocation, expectedUnityProject, expectedProcessId, expectedProcessStartedAtUtc, expectedTimeout);
        return invocation;
    }

    public static void EndpointWaitsUsedTimeouts (
        RecordingDaemonGuiSessionRegistrationAwaiter awaiter,
        params TimeSpan[] expectedTimeouts)
    {
        Assert.Equal(expectedTimeouts, awaiter.Invocations.Select(static invocation => invocation.RemainingTimeout).ToArray());
    }

    public static RecordingDaemonGuiRebootstrapClient.Invocation RebootstrapRequestedFor (
        RecordingDaemonGuiRebootstrapClient rebootstrapClient,
        ResolvedUnityProjectContext expectedUnityProject,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc,
        TimeSpan? expectedTimeout = null)
    {
        var invocation = Assert.Single(rebootstrapClient.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedProcessId, invocation.ExpectedProcessId);
        Assert.Equal(expectedProcessStartedAtUtc, invocation.ExpectedProcessStartedAtUtc);
        if (expectedTimeout.HasValue)
        {
            Assert.Equal(expectedTimeout.Value, invocation.RemainingTimeout);
        }

        return invocation;
    }

    private static void AssertEndpointWait (
        RecordingDaemonGuiSessionRegistrationAwaiter.Invocation invocation,
        ResolvedUnityProjectContext expectedUnityProject,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc,
        TimeSpan? expectedTimeout)
    {
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedProcessId, invocation.ExpectedProcessId);
        Assert.Equal(expectedProcessStartedAtUtc, invocation.ExpectedProcessStartedAtUtc);
        if (expectedTimeout.HasValue)
        {
            Assert.Equal(expectedTimeout.Value, invocation.RemainingTimeout);
        }
    }
}
