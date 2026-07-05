using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonStopServiceAssert
{
    public static void StopNotAttemptedAfterContextResolutionFailure (
        RecordingDaemonProjectLifecycleGateway supervisorProjectGateway,
        RecordingDaemonStopOperation daemonStopOperation)
    {
        Assert.Empty(supervisorProjectGateway.TryStopProjectInvocations);
        Assert.Empty(daemonStopOperation.Invocations);
    }

    public static RecordingDaemonProjectLifecycleGateway.TryStopProjectInvocation SupervisorStopCompletedWithoutDirectFallback (
        RecordingDaemonProjectLifecycleGateway supervisorProjectGateway,
        RecordingDaemonStopOperation daemonStopOperation,
        DaemonCommandExecutionContext context)
    {
        return AssertSupervisorStopHandledWithoutDirectFallback(
            supervisorProjectGateway,
            daemonStopOperation,
            context);
    }

    public static RecordingDaemonProjectLifecycleGateway.TryStopProjectInvocation SupervisorStopFailureStoppedBeforeDirectFallback (
        RecordingDaemonProjectLifecycleGateway supervisorProjectGateway,
        RecordingDaemonStopOperation daemonStopOperation,
        DaemonCommandExecutionContext context)
    {
        return AssertSupervisorStopHandledWithoutDirectFallback(
            supervisorProjectGateway,
            daemonStopOperation,
            context);
    }

    private static RecordingDaemonProjectLifecycleGateway.TryStopProjectInvocation AssertSupervisorStopHandledWithoutDirectFallback (
        RecordingDaemonProjectLifecycleGateway supervisorProjectGateway,
        RecordingDaemonStopOperation daemonStopOperation,
        DaemonCommandExecutionContext context)
    {
        var invocation = DaemonProjectLifecycleGatewayAssert.TryStopProjectRequested(
            supervisorProjectGateway,
            context.Context.UnityProject,
            context.Timeout);
        Assert.Empty(daemonStopOperation.Invocations);
        return invocation;
    }
}
