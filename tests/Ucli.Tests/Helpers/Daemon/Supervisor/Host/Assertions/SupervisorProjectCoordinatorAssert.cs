using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class SupervisorProjectCoordinatorAssert
{
    public static void StopProjectTimedOutWhileCompensationStopIsRunning (
        DaemonStopResult stopResult,
        SupervisorProjectCoordinator coordinator,
        RecordingDaemonStopOperation stopOperation,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        Assert.False(stopResult.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Timeout, stopResult.Error!.Kind);
        Assert.Equal(
            "Timed out while waiting for prior supervisor lifecycle cleanup to finish.",
            stopResult.Error.Message);
        DaemonStopOperationAssert.CompensationStopAttempted(
            stopOperation,
            expectedUnityProject,
            DaemonTimeouts.StopCompensationTimeout);
        Assert.True(coordinator.HasActiveProjectWork);
    }
}
