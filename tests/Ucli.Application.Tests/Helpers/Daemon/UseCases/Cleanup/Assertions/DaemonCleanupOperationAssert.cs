using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonCleanupOperationAssert
{
    public static void ContextResolutionFailureStoppedBeforeCleanup (
        DaemonCleanupExecutionResult result,
        RecordingDaemonCleanupOperation operation,
        ExecutionErrorKind expectedErrorKind)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(expectedErrorKind, error.Kind);
        Assert.Empty(operation.Invocations);
    }

    public static RecordingDaemonCleanupOperation.Invocation CleanupRequestedOnce (
        RecordingDaemonCleanupOperation operation,
        DaemonCommandExecutionContext context,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(operation.Invocations);
        Assert.Equal(context.Context.UnityProject, invocation.UnityProject);
        Assert.Equal(context.Timeout, invocation.Timeout);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        return invocation;
    }

}
