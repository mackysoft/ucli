using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Daemon;

internal static class DaemonCleanupOperationTestSupport
{
    public static DaemonCleanupOperation CreateOperation (
        IProjectLifecycleLockProvider? lifecycleLockProvider = null,
        IDaemonSessionStore? daemonSessionStore = null,
        IDaemonPingClient? daemonPingClient = null,
        IDaemonArtifactCleaner? artifactCleaner = null,
        IDaemonInvalidSessionCleanupSafetyEvaluator? invalidSessionCleanupSafetyEvaluator = null,
        IDaemonCleanupReachabilityProbe? cleanupReachabilityProbe = null)
    {
        var effectivePingClient = daemonPingClient ?? CreateSuccessfulPingClient();
        return new DaemonCleanupOperation(
            lifecycleLockProvider ?? new StubProjectLifecycleLockProvider(),
            daemonSessionStore ?? new RecordingDaemonSessionStore(),
            artifactCleaner ?? new RecordingDaemonArtifactCleaner(),
            invalidSessionCleanupSafetyEvaluator ?? new RecordingDaemonInvalidSessionCleanupSafetyEvaluator(),
            cleanupReachabilityProbe ?? new DaemonCleanupReachabilityProbe(effectivePingClient));
    }

    public static RecordingDaemonPingClient CreateSuccessfulPingClient ()
    {
        return CreatePingClient(static () => ValueTask.CompletedTask);
    }

    public static RecordingDaemonPingClient CreateNotRunningPingClient ()
    {
        return CreateFailingPingClient(new SocketException((int)SocketError.ConnectionRefused));
    }

    public static RecordingDaemonPingClient CreateFailingPingClient (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return CreatePingClient(() => ValueTask.FromException(exception));
    }

    public static RecordingDaemonPingClient CreatePingClient (Func<ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return new RecordingDaemonPingClient((_, _, _, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return handler();
        });
    }
}
