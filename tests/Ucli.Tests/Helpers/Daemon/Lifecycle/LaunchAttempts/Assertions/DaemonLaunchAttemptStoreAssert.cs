using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonLaunchAttemptStoreAssert
{
    public static DaemonLaunchAttempt LatestLaunchAttemptWrittenFor (
        RecordingDaemonLaunchAttemptStore launchAttemptStore,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        Assert.NotEmpty(launchAttemptStore.WriteInvocations);
        var invocation = launchAttemptStore.WriteInvocations[^1];
        return AssertLaunchAttemptWrite(invocation, expectedUnityProject);
    }

    public static DaemonLaunchAttempt LaunchAttemptRecordedAndPrunedFor (
        RecordingDaemonLaunchAttemptStore launchAttemptStore,
        ResolvedUnityProjectContext expectedUnityProject,
        Guid expectedLaunchAttemptId,
        DaemonStartupStatus expectedStartupStatus,
        DaemonStartupProcessAction expectedProcessAction)
    {
        var launchAttempt = LaunchAttemptRecordedFor(
            launchAttemptStore,
            expectedUnityProject,
            expectedLaunchAttemptId,
            expectedStartupStatus,
            expectedProcessAction);
        LaunchAttemptPrunedFor(launchAttemptStore, expectedUnityProject);
        return launchAttempt;
    }

    public static DaemonLaunchAttempt LaunchAttemptRecordedWithoutPruneFor (
        RecordingDaemonLaunchAttemptStore launchAttemptStore,
        ResolvedUnityProjectContext expectedUnityProject,
        Guid expectedLaunchAttemptId,
        DaemonStartupStatus expectedStartupStatus,
        DaemonStartupProcessAction expectedProcessAction)
    {
        var launchAttempt = LaunchAttemptRecordedFor(
            launchAttemptStore,
            expectedUnityProject,
            expectedLaunchAttemptId,
            expectedStartupStatus,
            expectedProcessAction);
        Assert.Empty(launchAttemptStore.PruneInvocations);
        return launchAttempt;
    }

    public static DaemonLaunchAttempt SingleLaunchAttemptRecordedAndPrunedFor (
        RecordingDaemonLaunchAttemptStore launchAttemptStore,
        ResolvedUnityProjectContext expectedUnityProject,
        Guid expectedLaunchAttemptId,
        DaemonStartupStatus expectedStartupStatus,
        DaemonStartupProcessAction expectedProcessAction)
    {
        var launchAttempt = LaunchAttemptRecordedAndPrunedFor(
            launchAttemptStore,
            expectedUnityProject,
            expectedLaunchAttemptId,
            expectedStartupStatus,
            expectedProcessAction);
        Assert.Single(launchAttemptStore.WriteInvocations);
        return launchAttempt;
    }

    public static DaemonLaunchAttempt SingleLaunchAttemptRecordedWithoutPruneFor (
        RecordingDaemonLaunchAttemptStore launchAttemptStore,
        ResolvedUnityProjectContext expectedUnityProject,
        Guid expectedLaunchAttemptId,
        DaemonStartupStatus expectedStartupStatus,
        DaemonStartupProcessAction expectedProcessAction)
    {
        var launchAttempt = LaunchAttemptRecordedWithoutPruneFor(
            launchAttemptStore,
            expectedUnityProject,
            expectedLaunchAttemptId,
            expectedStartupStatus,
            expectedProcessAction);
        Assert.Single(launchAttemptStore.WriteInvocations);
        return launchAttempt;
    }

    public static void LaunchAttemptPrunedFor (
        RecordingDaemonLaunchAttemptStore launchAttemptStore,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        var invocation = Assert.Single(launchAttemptStore.PruneInvocations);
        Assert.Equal(expectedUnityProject.RepositoryRoot, invocation.StorageRoot);
        Assert.Equal(expectedUnityProject.ProjectFingerprint, invocation.ProjectFingerprint);
        Assert.True(invocation.KeepCount > 0);
    }

    private static DaemonLaunchAttempt LaunchAttemptRecordedFor (
        RecordingDaemonLaunchAttemptStore launchAttemptStore,
        ResolvedUnityProjectContext expectedUnityProject,
        Guid expectedLaunchAttemptId,
        DaemonStartupStatus expectedStartupStatus,
        DaemonStartupProcessAction expectedProcessAction)
    {
        var launchAttempt = LatestLaunchAttemptWrittenFor(launchAttemptStore, expectedUnityProject);
        Assert.Equal(expectedLaunchAttemptId, launchAttempt.LaunchAttemptId);
        Assert.Equal(expectedStartupStatus, launchAttempt.StartupStatus);
        Assert.Equal(expectedProcessAction, launchAttempt.ProcessAction);
        return launchAttempt;
    }

    private static DaemonLaunchAttempt AssertLaunchAttemptWrite (
        RecordingDaemonLaunchAttemptStore.WriteInvocation invocation,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        Assert.Equal(expectedUnityProject.RepositoryRoot, invocation.StorageRoot);
        Assert.Equal(expectedUnityProject.ProjectFingerprint, invocation.ProjectFingerprint);
        return invocation.LaunchAttempt;
    }
}
