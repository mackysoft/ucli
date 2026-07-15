using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
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
        TimeProvider timeProvider,
        IDaemonDiagnosisStore? daemonDiagnosisStore = null,
        IUnityGuiEditorProcessLauncher? unityGuiEditorProcessLauncher = null,
        IDaemonGuiStartupObserver? guiStartupObserver = null,
        IGuidGenerator? launchAttemptIdGenerator = null,
        IDaemonLaunchAttemptStore? launchAttemptStore = null,
        DaemonCompensationOperationOwner? compensationOperationOwner = null)
    {
        return new DaemonLaunchService(
            daemonLaunchSessionService: launchSessionService,
            unityDaemonProcessLauncher: unityDaemonProcessLauncher,
            unityGuiEditorProcessLauncher: unityGuiEditorProcessLauncher ?? new RecordingUnityGuiEditorProcessLauncher(),
            startupReadinessProbe: startupReadinessProbe,
            guiStartupObserver: guiStartupObserver ?? new RecordingDaemonGuiStartupObserver(),
            daemonLaunchCompensationService: launchCompensationService,
            daemonDiagnosisStore: daemonDiagnosisStore ?? new RecordingDaemonDiagnosisStore(),
            launchAttemptIdGenerator: launchAttemptIdGenerator ?? new SequentialGuidGenerator(),
            launchAttemptStore: launchAttemptStore ?? new RecordingDaemonLaunchAttemptStore(),
            compensationOperationOwner: compensationOperationOwner ?? new DaemonCompensationOperationOwner(),
            timeProvider: timeProvider);
    }

    public static Guid AssertStartupLaunchAttemptId (DaemonStartupObservation? startup)
    {
        Assert.NotNull(startup);
        Assert.True(startup!.LaunchAttemptId.HasValue);
        Assert.NotEqual(Guid.Empty, startup.LaunchAttemptId.Value);
        return startup.LaunchAttemptId.Value;
    }
}
