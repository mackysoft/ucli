using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonLaunchServiceTestSupport
{
    public const string LaunchSessionToken = "session-token";

    public const string LaunchEndpointAddress = "ucli-daemon-test-endpoint";

    public static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    public static DaemonLaunchService CreateService (
        IDaemonLaunchSessionService launchSessionService,
        IUnityDaemonProcessLauncher unityDaemonProcessLauncher,
        IDaemonStartupReadinessProbe startupReadinessProbe,
        IDaemonLaunchCompensationService launchCompensationService,
        IDaemonDiagnosisStore? daemonDiagnosisStore = null,
        IUnityGuiEditorProcessLauncher? unityGuiEditorProcessLauncher = null,
        IDaemonGuiStartupObserver? guiStartupObserver = null,
        IDaemonLaunchAttemptIdGenerator? launchAttemptIdGenerator = null,
        IDaemonLaunchAttemptStore? launchAttemptStore = null,
        TimeProvider? timeProvider = null)
    {
        return new DaemonLaunchService(
            daemonLaunchSessionService: launchSessionService,
            unityDaemonProcessLauncher: unityDaemonProcessLauncher,
            unityGuiEditorProcessLauncher: unityGuiEditorProcessLauncher ?? new RecordingUnityGuiEditorProcessLauncher(),
            startupReadinessProbe: startupReadinessProbe,
            guiStartupObserver: guiStartupObserver ?? new RecordingDaemonGuiStartupObserver(),
            daemonLaunchCompensationService: launchCompensationService,
            daemonDiagnosisStore: daemonDiagnosisStore ?? new RecordingDaemonDiagnosisStore(),
            launchAttemptIdGenerator: launchAttemptIdGenerator ?? new SequentialDaemonLaunchAttemptIdGenerator(),
            launchAttemptStore: launchAttemptStore ?? new RecordingDaemonLaunchAttemptStore(),
            timeProvider: timeProvider);
    }

    public static string AssertStartupLaunchAttemptId (DaemonStartupObservation? startup)
    {
        Assert.NotNull(startup);
        Assert.NotNull(startup!.LaunchAttemptId);
        return startup.LaunchAttemptId;
    }
}
