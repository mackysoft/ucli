using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonListQueryServiceAssert
{
    public static void ContextResolutionFailureStoppedBeforeListQuery (
        DaemonListExecutionResult result,
        RecordingDaemonListQueryService queryService,
        string expectedErrorMessage)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal(expectedErrorMessage, result.Error!.Message);
        Assert.Empty(queryService.Invocations);
    }

    public static RecordingDaemonListQueryService.Invocation ListRequestedOnce (
        RecordingDaemonListQueryService queryService,
        DaemonCommandExecutionContext context,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(queryService.Invocations);
        Assert.Equal(context.Context.UnityProject, invocation.UnityProject);
        Assert.Equal(context.Timeout, invocation.Timeout);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        return invocation;
    }

}
