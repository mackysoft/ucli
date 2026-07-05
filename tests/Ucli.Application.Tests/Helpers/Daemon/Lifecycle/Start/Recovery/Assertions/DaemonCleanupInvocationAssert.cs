namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonCleanupInvocationAssert
{
    public static void AssertProcessTerminationAttempted (
        RecordingDaemonProcessTerminationService processTerminationService,
        int processId,
        DateTimeOffset? processStartedAtUtc,
        TimeSpan? timeout = null)
    {
        var invocation = Assert.Single(processTerminationService.Invocations);
        var target = AssertTerminationTarget(invocation.Target);
        Assert.Equal(processId, target.ProcessId);
        Assert.Equal(processStartedAtUtc, target.ProcessStartedAtUtc);
        if (timeout.HasValue)
        {
            Assert.Equal(timeout.Value, invocation.Timeout);
        }
    }

    public static void AssertSessionArtifactsInvalidated (
        RecordingDaemonArtifactCleaner artifactCleaner,
        ResolvedUnityProjectContext context)
    {
        var invocation = Assert.Single(artifactCleaner.Invocations);
        Assert.Equal(context, invocation.UnityProject);
    }

    public static void AssertProcessTerminationAttemptedThenArtifactsInvalidated (
        RecordingDaemonProcessTerminationService processTerminationService,
        RecordingDaemonArtifactCleaner artifactCleaner,
        ResolvedUnityProjectContext context,
        int processId,
        DateTimeOffset? processStartedAtUtc,
        TimeSpan? timeout = null)
    {
        AssertProcessTerminationAttempted(processTerminationService, processId, processStartedAtUtc, timeout);
        AssertSessionArtifactsInvalidated(artifactCleaner, context);
    }

    public static void AssertProcessTerminationAttemptedWithoutArtifactCleanup (
        RecordingDaemonProcessTerminationService processTerminationService,
        RecordingDaemonArtifactCleaner artifactCleaner,
        int processId,
        DateTimeOffset? processStartedAtUtc,
        TimeSpan? timeout = null)
    {
        AssertProcessTerminationAttempted(processTerminationService, processId, processStartedAtUtc, timeout);
        Assert.Empty(artifactCleaner.Invocations);
    }

    public static void AssertSessionArtifactsInvalidatedWithoutProcessTermination (
        RecordingDaemonProcessTerminationService processTerminationService,
        RecordingDaemonArtifactCleaner artifactCleaner,
        ResolvedUnityProjectContext context)
    {
        Assert.Empty(processTerminationService.Invocations);
        AssertSessionArtifactsInvalidated(artifactCleaner, context);
    }

    public static void AssertProcessTerminationAndArtifactCleanupSkipped (
        RecordingDaemonProcessTerminationService processTerminationService,
        RecordingDaemonArtifactCleaner artifactCleaner)
    {
        Assert.Empty(processTerminationService.Invocations);
        Assert.Empty(artifactCleaner.Invocations);
    }

    private static DaemonProcessTerminationTarget AssertTerminationTarget (DaemonProcessTerminationTarget? target)
    {
        Assert.True(target.HasValue);
        return target.Value;
    }
}
