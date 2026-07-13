using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonStartOperationInvocationAssert
{
    private static void InvalidSessionCleanupAttempted (
        RecordingDaemonSessionCleanupService cleanupService,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSessionReadResult expectedReadResult)
    {
        var invocation = Assert.Single(cleanupService.InvalidSessionInvocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedReadResult, invocation.ReadResult);
        Assert.Empty(cleanupService.StaleSessionInvocations);
    }

    public static void InvalidSessionCleanupAttemptedBeforeFreshLaunch (
        RecordingDaemonSessionCleanupService cleanupService,
        RecordingDaemonExistingSessionGateService existingSessionGateService,
        RecordingDaemonLaunchService launchService,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSessionReadResult expectedReadResult)
    {
        InvalidSessionCleanupAttempted(cleanupService, expectedUnityProject, expectedReadResult);
        ExistingSessionGateSkipped(existingSessionGateService);
        FreshLaunchAttempted(launchService, expectedUnityProject);
    }

    public static void InvalidSessionCleanupFailureStoppedBeforeGateOrLaunch (
        RecordingDaemonSessionCleanupService cleanupService,
        RecordingDaemonExistingSessionGateService existingSessionGateService,
        RecordingDaemonLaunchService launchService,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSessionReadResult expectedReadResult)
    {
        InvalidSessionCleanupAttempted(cleanupService, expectedUnityProject, expectedReadResult);
        ExistingSessionGateSkipped(existingSessionGateService);
        FreshLaunchSkipped(launchService);
    }

    public static void UnsafeInvalidSessionCleanupSkippedBeforeLaunch (
        RecordingDaemonProcessTerminationService processTerminationService,
        RecordingDaemonArtifactCleaner artifactCleaner,
        RecordingDaemonLaunchService launchService)
    {
        Assert.Empty(processTerminationService.Invocations);
        Assert.Empty(artifactCleaner.Invocations);
        FreshLaunchSkipped(launchService);
    }

    public static void SessionReadFailureStoppedBeforeRecoveryOrLaunch (
        RecordingDaemonSessionCleanupService cleanupService,
        RecordingDaemonExistingSessionGateService existingSessionGateService,
        RecordingDaemonLaunchService launchService)
    {
        Assert.Empty(cleanupService.InvalidSessionInvocations);
        Assert.Empty(cleanupService.StaleSessionInvocations);
        ExistingSessionGateSkipped(existingSessionGateService);
        FreshLaunchSkipped(launchService);
    }

    public static void StaleSessionCleanupAttemptedFor (
        RecordingDaemonSessionCleanupService cleanupService,
        DaemonSession expectedSession)
    {
        var invocation = Assert.Single(cleanupService.StaleSessionInvocations);
        Assert.Equal(expectedSession, invocation.Session);
    }

    public static void StaleSessionCleanupAttemptedWithTimeoutLessThan (
        RecordingDaemonSessionCleanupService cleanupService,
        DaemonSession expectedSession,
        TimeSpan maximumTimeout)
    {
        var invocation = Assert.Single(cleanupService.StaleSessionInvocations);
        Assert.Equal(expectedSession, invocation.Session);
        Assert.True(invocation.Timeout < maximumTimeout);
    }

    public static RecordingDaemonExistingSessionGateService.Invocation ExistingSessionGateAttempted (
        RecordingDaemonExistingSessionGateService existingSessionGateService,
        ResolvedUnityProjectContext? expectedUnityProject = null,
        DaemonSession? expectedSession = null)
    {
        var invocation = Assert.Single(existingSessionGateService.Invocations);
        if (expectedUnityProject is not null)
        {
            Assert.Equal(expectedUnityProject, invocation.UnityProject);
        }

        if (expectedSession is not null)
        {
            Assert.Equal(expectedSession, invocation.Session);
        }

        Assert.True(invocation.Timeout > TimeSpan.Zero);
        return invocation;
    }

    public static RecordingDaemonExistingSessionGateService.Invocation ExistingSessionReturnedWithoutFreshLaunch (
        RecordingDaemonSessionCleanupService cleanupService,
        RecordingDaemonExistingSessionGateService existingSessionGateService,
        RecordingDaemonLaunchService launchService,
        DaemonSession expectedSession)
    {
        SessionCleanupSkipped(cleanupService);
        var invocation = ExistingSessionGateAttempted(existingSessionGateService, expectedSession: expectedSession);
        FreshLaunchSkipped(launchService);
        return invocation;
    }

    public static RecordingDaemonExistingSessionGateService.Invocation ExistingSessionGateAttemptedWithoutFreshLaunch (
        RecordingDaemonExistingSessionGateService existingSessionGateService,
        RecordingDaemonLaunchService launchService,
        DaemonSession expectedSession)
    {
        var invocation = ExistingSessionGateAttempted(existingSessionGateService, expectedSession: expectedSession);
        FreshLaunchSkipped(launchService);
        return invocation;
    }

    public static void ExistingSessionTookPrecedenceOverGuiAttachAndFreshLaunch (
        RecordingDaemonGuiEditorAttachService guiAttachService,
        RecordingDaemonLaunchService launchService)
    {
        Assert.Empty(guiAttachService.Invocations);
        FreshLaunchSkipped(launchService);
    }

    public static RecordingDaemonGuiEditorAttachService.Invocation GuiAttachAttempted (
        RecordingDaemonGuiEditorAttachService guiAttachService,
        ResolvedUnityProjectContext? expectedUnityProject = null)
    {
        var invocation = Assert.Single(guiAttachService.Invocations);
        if (expectedUnityProject is not null)
        {
            Assert.Equal(expectedUnityProject, invocation.UnityProject);
        }

        Assert.True(invocation.Timeout > TimeSpan.Zero);
        return invocation;
    }

    public static RecordingDaemonGuiEditorAttachService.Invocation GuiAttachReturnedWithoutFreshLaunch (
        RecordingDaemonGuiEditorAttachService guiAttachService,
        RecordingDaemonLaunchService launchService,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        var invocation = GuiAttachAttempted(guiAttachService, expectedUnityProject);
        FreshLaunchSkipped(launchService);
        return invocation;
    }

    public static RecordingDaemonLaunchService.Invocation FreshLaunchAttempted (
        RecordingDaemonLaunchService launchService,
        ResolvedUnityProjectContext? expectedUnityProject = null,
        DaemonEditorMode? expectedEditorMode = null,
        DaemonStartupBlockedProcessPolicy? expectedStartupBlockedPolicy = null)
    {
        var invocation = Assert.Single(launchService.Invocations);
        if (expectedUnityProject is not null)
        {
            Assert.Equal(expectedUnityProject, invocation.UnityProject);
        }

        Assert.True(invocation.Timeout > TimeSpan.Zero);
        if (expectedEditorMode.HasValue)
        {
            Assert.Equal(expectedEditorMode.Value, invocation.EditorMode);
        }

        if (expectedStartupBlockedPolicy.HasValue)
        {
            Assert.Equal(expectedStartupBlockedPolicy.Value, invocation.OnStartupBlocked);
        }

        return invocation;
    }

    public static RecordingDaemonLaunchService.Invocation FreshLaunchAttemptedWithoutExistingSessionGate (
        RecordingDaemonExistingSessionGateService existingSessionGateService,
        RecordingDaemonLaunchService launchService,
        ResolvedUnityProjectContext? expectedUnityProject = null,
        DaemonEditorMode? expectedEditorMode = null)
    {
        ExistingSessionGateSkipped(existingSessionGateService);
        return FreshLaunchAttempted(launchService, expectedUnityProject, expectedEditorMode);
    }

    private static void SessionCleanupSkipped (RecordingDaemonSessionCleanupService cleanupService)
    {
        Assert.Empty(cleanupService.InvalidSessionInvocations);
        Assert.Empty(cleanupService.StaleSessionInvocations);
    }

    private static void ExistingSessionGateSkipped (RecordingDaemonExistingSessionGateService existingSessionGateService)
    {
        Assert.Empty(existingSessionGateService.Invocations);
    }

    private static void FreshLaunchSkipped (RecordingDaemonLaunchService launchService)
    {
        Assert.Empty(launchService.Invocations);
    }
}
