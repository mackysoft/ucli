using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonSessionAcquisitionCoordinatorTestFactory
{
    public static DaemonSessionAcquisitionCoordinator Create (
        IDaemonSessionStore sessionStore,
        DaemonSessionRecoveryWaiter? recoveryWaiter = null)
    {
        ArgumentNullException.ThrowIfNull(sessionStore);
        return new DaemonSessionAcquisitionCoordinator(
            sessionStore,
            recoveryWaiter ?? new DaemonSessionRecoveryWaiter(
                new RecordingDaemonLifecycleStore(),
                new RecordingDaemonProcessIdentityAssessor(
                    DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess)));
    }
}
