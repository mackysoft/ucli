using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonLaunchInvocationAssert
{
    public static RecordingDaemonLaunchSessionService.InitializeInvocation SessionInitializedFor (
        RecordingDaemonLaunchSessionService launchSessionService,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonEditorMode expectedEditorMode)
    {
        var invocation = Assert.Single(launchSessionService.InitializeInvocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedEditorMode, invocation.EditorMode);
        return invocation;
    }

    public static RecordingUnityDaemonProcessLauncher.Invocation BatchmodeLaunchStartedFor (
        RecordingUnityDaemonProcessLauncher launcher,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSession expectedSession)
    {
        var invocation = Assert.Single(launcher.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedSession, invocation.Session);
        return invocation;
    }

    public static RecordingUnityGuiEditorProcessLauncher.Invocation GuiLaunchStartedFor (
        RecordingUnityGuiEditorProcessLauncher launcher,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        var invocation = Assert.Single(launcher.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        return invocation;
    }

    public static RecordingDaemonGuiStartupObserver.Invocation GuiSessionRegistrationWaitedFor (
        RecordingDaemonGuiStartupObserver guiStartupObserver,
        ResolvedUnityProjectContext expectedUnityProject,
        int expectedProcessId)
    {
        var invocation = Assert.Single(guiStartupObserver.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedProcessId, invocation.ProcessId);
        Assert.True(invocation.Timeout > TimeSpan.Zero);
        return invocation;
    }

    public static RecordingDaemonGuiStartupObserver.Invocation LatestGuiStartupInvocation (
        RecordingDaemonGuiStartupObserver guiStartupObserver)
    {
        Assert.NotEmpty(guiStartupObserver.Invocations);
        return guiStartupObserver.Invocations[^1];
    }

    public static RecordingDaemonLaunchSessionService.UpdateProcessIdInvocation ProcessIdUpdatedFor (
        RecordingDaemonLaunchSessionService launchSessionService,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSession expectedSession,
        int processId,
        DateTimeOffset processStartedAtUtc)
    {
        var invocation = Assert.Single(launchSessionService.UpdateProcessIdInvocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedSession, invocation.Session);
        Assert.Equal(processId, invocation.ProcessId);
        Assert.Equal(processStartedAtUtc, invocation.ProcessStartedAtUtc);
        return invocation;
    }

    public static void BatchmodeLaunchCompletedWithoutCompensationOrDiagnosis (
        RecordingDaemonLaunchSessionService launchSessionService,
        RecordingUnityDaemonProcessLauncher launcher,
        RecordingDaemonLaunchCompensationService compensationService,
        RecordingDaemonDiagnosisStore diagnosisStore,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSession expectedInitialSession,
        int processId,
        DateTimeOffset processStartedAtUtc)
    {
        SessionInitializedFor(launchSessionService, expectedUnityProject, DaemonEditorMode.Batchmode);
        BatchmodeLaunchStartedFor(launcher, expectedUnityProject, expectedInitialSession);
        ProcessIdUpdatedFor(
            launchSessionService,
            expectedUnityProject,
            expectedInitialSession,
            processId,
            processStartedAtUtc);
        Assert.Empty(compensationService.Invocations);
        Assert.Empty(diagnosisStore.WriteInvocations);
    }

    public static void GuiLaunchCompletedWithoutPrewrittenSessionOrCompensation (
        RecordingDaemonLaunchSessionService launchSessionService,
        RecordingUnityDaemonProcessLauncher batchmodeLauncher,
        RecordingUnityGuiEditorProcessLauncher guiLauncher,
        RecordingDaemonGuiStartupObserver guiStartupObserver,
        RecordingDaemonLaunchCompensationService compensationService,
        RecordingDaemonDiagnosisStore diagnosisStore,
        ResolvedUnityProjectContext expectedUnityProject,
        int processId)
    {
        Assert.Empty(launchSessionService.InitializeInvocations);
        Assert.Empty(launchSessionService.UpdateProcessIdInvocations);
        Assert.Empty(batchmodeLauncher.Invocations);
        GuiLaunchStartedFor(guiLauncher, expectedUnityProject);
        GuiSessionRegistrationWaitedFor(guiStartupObserver, expectedUnityProject, processId);
        Assert.Empty(compensationService.Invocations);
        Assert.Empty(diagnosisStore.WriteInvocations);
    }

    public static void BatchmodeLaunchStoppedAfterSessionInitializationFailure (
        RecordingDaemonLaunchSessionService launchSessionService,
        RecordingUnityDaemonProcessLauncher launcher,
        RecordingDaemonLaunchCompensationService compensationService,
        RecordingDaemonDiagnosisStore diagnosisStore,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        SessionInitializedFor(launchSessionService, expectedUnityProject, DaemonEditorMode.Batchmode);
        Assert.Empty(launchSessionService.UpdateProcessIdInvocations);
        Assert.Empty(launcher.Invocations);
        Assert.Empty(compensationService.Invocations);
        Assert.Empty(diagnosisStore.WriteInvocations);
    }

    public static DaemonStartupObservation StartupFailureKeptProcessWithoutCompensation (
        DaemonStartResult result,
        RecordingDaemonLaunchCompensationService compensationService)
    {
        Assert.Equal(DaemonStartStatus.Failed, result.Status);
        Assert.NotNull(result.Startup);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Kept), result.Startup!.ProcessAction);
        Assert.Empty(compensationService.Invocations);
        return result.Startup;
    }

    public static RecordingDaemonLaunchCompensationService.Invocation LaunchCompensationAttemptedBeforeProcessIdWasRecorded (
        RecordingDaemonLaunchCompensationService compensationService,
        RecordingDaemonLaunchSessionService launchSessionService,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        var invocation = LaunchCompensationAttemptedWithoutProcessTarget(compensationService, expectedUnityProject);
        Assert.Empty(launchSessionService.UpdateProcessIdInvocations);
        return invocation;
    }

    public static RecordingDaemonLaunchCompensationService.Invocation LaunchCompensationAttemptedWithoutDiagnosisWrite (
        RecordingDaemonLaunchCompensationService compensationService,
        RecordingDaemonDiagnosisStore diagnosisStore,
        ResolvedUnityProjectContext expectedUnityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        TimeSpan timeout)
    {
        var invocation = LaunchCompensationAttempted(
            compensationService,
            expectedUnityProject,
            processId,
            processStartedAtUtc,
            timeout);
        Assert.Empty(diagnosisStore.WriteInvocations);
        return invocation;
    }

    public static RecordingDaemonLaunchCompensationService.Invocation LaunchCompensationAttemptedWithoutProcessTarget (
        RecordingDaemonLaunchCompensationService compensationService,
        ResolvedUnityProjectContext expectedUnityProject,
        TimeSpan? timeout = null)
    {
        var invocation = Assert.Single(compensationService.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Null(invocation.Target);
        if (timeout.HasValue)
        {
            Assert.True(invocation.Timeout > TimeSpan.Zero);
            Assert.True(invocation.Timeout <= timeout.Value);
        }

        return invocation;
    }

    public static RecordingDaemonLaunchCompensationService.Invocation LaunchCompensationAttempted (
        RecordingDaemonLaunchCompensationService compensationService,
        ResolvedUnityProjectContext expectedUnityProject,
        int processId,
        DateTimeOffset processStartedAtUtc,
        TimeSpan? timeout = null)
    {
        var invocation = Assert.Single(compensationService.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        var target = CompensationTarget(invocation.Target);
        Assert.Equal(processId, target.ProcessId);
        Assert.Equal(processStartedAtUtc, target.ProcessStartedAtUtc);
        if (timeout.HasValue)
        {
            Assert.True(invocation.Timeout > TimeSpan.Zero);
            Assert.True(invocation.Timeout <= timeout.Value);
        }

        return invocation;
    }

    private static DaemonProcessTerminationTarget CompensationTarget (DaemonProcessTerminationTarget? target)
    {
        Assert.True(target.HasValue);
        return target.Value;
    }
}
