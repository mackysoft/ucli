using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;

namespace MackySoft.Ucli.Application.Tests;

internal static class UnityConsoleClearClientAssert
{
    public static RecordingUnityConsoleClearClient.Invocation ClearRequestedOnce (
        RecordingUnityConsoleClearClient client,
        DaemonCommandExecutionContext context,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(client.Invocations);
        Assert.Equal(context.Context.UnityProject, invocation.UnityProject);
        Assert.Equal(context.Timeout, invocation.Timeout);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        return invocation;
    }

}
