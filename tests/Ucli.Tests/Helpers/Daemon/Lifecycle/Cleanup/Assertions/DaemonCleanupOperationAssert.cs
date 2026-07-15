using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonCleanupOperationAssert
{
    public static void CompletedAfterArtifactCleanup (
        DaemonCleanupResult result,
        RecordingDaemonArtifactCleaner artifactCleaner,
        ResolvedUnityProjectContext expectedUnityProject,
        int expectedDeletedLaunchAttemptCount = 0)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Completed, result.Status);
        Assert.Null(result.SkipReason);
        Assert.Equal(expectedDeletedLaunchAttemptCount, result.DeletedLaunchAttemptCount);
        Assert.Null(result.Error);

        var invocation = Assert.Single(artifactCleaner.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
    }

    public static void SkippedWithoutArtifactCleanup (
        DaemonCleanupResult result,
        RecordingDaemonArtifactCleaner artifactCleaner,
        DaemonCleanupSkipReason expectedSkipReason)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonCleanupStatus.Skipped, result.Status);
        Assert.Equal(expectedSkipReason, result.SkipReason);
        Assert.Equal(0, result.DeletedLaunchAttemptCount);
        Assert.Null(result.Error);
        Assert.Empty(artifactCleaner.Invocations);
    }

    public static ExecutionError FailedWithoutArtifactCleanup (
        DaemonCleanupResult result,
        RecordingDaemonArtifactCleaner artifactCleaner,
        ExecutionErrorKind expectedErrorKind)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Status);
        Assert.Null(result.SkipReason);
        Assert.Equal(0, result.DeletedLaunchAttemptCount);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(expectedErrorKind, error.Kind);
        Assert.Empty(artifactCleaner.Invocations);
        return error;
    }
}
