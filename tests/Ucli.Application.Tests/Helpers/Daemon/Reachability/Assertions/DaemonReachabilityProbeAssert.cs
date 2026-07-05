using MackySoft.Tests;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonReachabilityProbeAssert
{
    public static RecordingDaemonReachabilityProbe.Invocation ProbeAttemptedFor (
        RecordingDaemonReachabilityProbe probe,
        ResolvedUnityProjectContext expectedUnityProject,
        TimeSpan expectedTimeout)
    {
        var invocation = Assert.Single(probe.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        return invocation;
    }

    public static async Task CancellationRejectedBeforeProbeAsync (
        Task decisionTask,
        RecordingDaemonReachabilityProbe probe,
        TimeSpan waitTimeout)
    {
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                decisionTask,
                "Canceled unity execution mode decision",
                waitTimeout);
        });
        Assert.Empty(probe.Invocations);
    }

    public static async Task InvalidTimeoutRejectedBeforeProbeAsync (
        Task decisionTask,
        RecordingDaemonReachabilityProbe probe,
        TimeSpan waitTimeout)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                decisionTask,
                "Invalid timeout unity execution mode decision",
                waitTimeout);
        });
        Assert.Empty(probe.Invocations);
    }
}
