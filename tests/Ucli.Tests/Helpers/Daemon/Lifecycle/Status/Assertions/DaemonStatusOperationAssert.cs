using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonStatusOperationAssert
{
    public static void StaleSessionReturnedWithoutDiagnosisWrite (
        DaemonStatusResult result,
        DaemonSession expectedSession,
        RecordingDaemonDiagnosisStore diagnosisStore)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStatusKind.Stale, result.Status);
        Assert.Equal(expectedSession, result.Session);
        Assert.Null(result.Diagnosis);
        Assert.Null(result.Error);
        Assert.Empty(diagnosisStore.WriteInvocations);
    }
}
