using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonShutdownClientAssert
{
    public static void EndpointShutdownAttempted (
        RecordingDaemonShutdownClient shutdownClient,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSession expectedSession)
    {
        var invocation = Assert.Single(shutdownClient.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedSession, invocation.Session);
    }
}
