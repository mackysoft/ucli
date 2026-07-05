using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonExistingSessionGateServiceAssert
{
    public static void RecoveringSessionReturnedAlreadyRunningWithoutStaleCleanup (
        DaemonStartResult? result,
        DaemonSession expectedSession,
        RecordingDaemonSessionCleanupService cleanupService)
    {
        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.AlreadyRunning, result!.Status);
        Assert.Equal(expectedSession, result.Session);
        Assert.Empty(cleanupService.StaleSessionInvocations);
    }

    public static void CleanupDeadlineFailureReturnedWithoutStaleCleanup (
        DaemonStartResult? result,
        RecordingDaemonSessionCleanupService cleanupService)
    {
        Assert.NotNull(result);
        Assert.Equal(DaemonStartStatus.Failed, result!.Status);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Empty(cleanupService.StaleSessionInvocations);
    }
}
