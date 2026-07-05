using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Tests.Daemon;

internal static class DaemonStartOperationTestSupport
{
    public static DaemonStartOperation CreateOperation (
        IDaemonSessionStore daemonSessionStore,
        IDaemonSessionCleanupService daemonSessionCleanupService,
        IDaemonExistingSessionGateService daemonExistingSessionGateService,
        IDaemonLaunchService daemonLaunchService,
        IDaemonDiagnosisStore? daemonDiagnosisStore = null,
        IDaemonGuiEditorAttachService? daemonGuiEditorAttachService = null,
        IProjectLifecycleLockProvider? lifecycleLockProvider = null)
    {
        return new DaemonStartOperation(
            lifecycleLockProvider: lifecycleLockProvider ?? new StubProjectLifecycleLockProvider(),
            daemonDiagnosisStore: daemonDiagnosisStore ?? new RecordingDaemonDiagnosisStore(),
            daemonSessionStore: daemonSessionStore,
            daemonSessionCleanupService: daemonSessionCleanupService,
            daemonExistingSessionGateService: daemonExistingSessionGateService,
            daemonGuiEditorAttachService: daemonGuiEditorAttachService ?? new RecordingDaemonGuiEditorAttachService(),
            daemonLaunchService: daemonLaunchService);
    }
}
