using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonStopOperationAssert
{
    public static RecordingDaemonStopOperation.Invocation StopRequested (
        RecordingDaemonStopOperation daemonStopOperation,
        DaemonCommandExecutionContext context)
    {
        var invocation = Assert.Single(daemonStopOperation.Invocations);
        Assert.Equal(context.Context.UnityProject, invocation.UnityProject);
        Assert.True(invocation.Timeout > TimeSpan.Zero);
        Assert.True(invocation.Timeout <= context.Timeout);
        return invocation;
    }

    public static RecordingDaemonStopOperation.Invocation CompensationStopAttempted (
        RecordingDaemonStopOperation stopOperation,
        ResolvedUnityProjectContext expectedUnityProject,
        TimeSpan expectedTimeout)
    {
        var invocation = Assert.Single(stopOperation.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        return invocation;
    }
}
