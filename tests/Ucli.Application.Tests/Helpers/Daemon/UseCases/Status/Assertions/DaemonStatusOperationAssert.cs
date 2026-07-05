using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonStatusOperationAssert
{
    public static RecordingDaemonStatusOperation.Invocation StatusRequested (
        RecordingDaemonStatusOperation daemonStatusOperation,
        ProjectContext expectedContext,
        TimeSpan expectedTimeout,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(daemonStatusOperation.Invocations);
        Assert.Equal(expectedContext.UnityProject, invocation.UnityProject);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        return invocation;
    }

}
