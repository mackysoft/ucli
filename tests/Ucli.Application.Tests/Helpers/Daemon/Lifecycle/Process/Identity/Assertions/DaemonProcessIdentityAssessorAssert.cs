using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonProcessIdentityAssessorAssert
{
    public static RecordingDaemonProcessIdentityAssessor.Invocation AssessedOnceForSession (
        RecordingDaemonProcessIdentityAssessor assessor,
        DaemonSession session)
    {
        var invocation = Assert.Single(assessor.Invocations);
        Assert.Equal(session.ProcessId, invocation.ProcessId);
        Assert.Equal(session.ProcessStartedAtUtc, invocation.ExpectedProcessStartedAtUtc);
        return invocation;
    }
}
