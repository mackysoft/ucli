using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonStatusServiceInvocationAssert
{
    public static void StatusCommandResolvedAndOperationExecuted (
        RecordingDaemonCommandExecutionContextResolver resolver,
        RecordingDaemonStatusOperation daemonStatusOperation,
        DaemonCommandExecutionContext context,
        string? expectedProjectPath,
        int? expectedTimeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var resolverInvocation = Assert.Single(resolver.Invocations);
        Assert.Equal(UcliCommandIds.DaemonStatus, resolverInvocation.TimeoutCommand);
        Assert.Equal(expectedProjectPath, resolverInvocation.ProjectPath);
        Assert.Equal(expectedTimeoutMilliseconds, resolverInvocation.TimeoutMilliseconds);
        Assert.Equal(cancellationToken, resolverInvocation.CancellationToken);

        var operationInvocation = Assert.Single(daemonStatusOperation.Invocations);
        Assert.Equal(context.Context.UnityProject, operationInvocation.UnityProject);
        Assert.Equal(context.Timeout, operationInvocation.Timeout);
        Assert.Equal(cancellationToken, operationInvocation.CancellationToken);
    }
}
