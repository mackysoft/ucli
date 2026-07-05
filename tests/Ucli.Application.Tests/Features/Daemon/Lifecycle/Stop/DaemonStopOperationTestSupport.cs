using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

namespace MackySoft.Ucli.Application.Tests.Daemon;

internal static class DaemonStopOperationTestSupport
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(500);

    public static RecordingDaemonSessionStore CreateSessionStore (DaemonSession? session)
    {
        return new RecordingDaemonSessionStore(DaemonSessionReadResult.Success(session));
    }

    public static DaemonStopOperation CreateOperation (
        StubProjectLifecycleLockProvider? lifecycleLockProvider = null,
        RecordingDaemonSessionStore? sessionStore = null,
        RecordingDaemonShutdownClient? shutdownClient = null,
        RecordingDaemonProcessTerminationService? processTerminationService = null,
        RecordingDaemonArtifactCleaner? artifactCleaner = null,
        TimeProvider? timeProvider = null)
    {
        return new DaemonStopOperation(
            lifecycleLockProvider: lifecycleLockProvider ?? new StubProjectLifecycleLockProvider(),
            daemonSessionStore: sessionStore ?? CreateSessionStore(null),
            shutdownClient: shutdownClient ?? new RecordingDaemonShutdownClient(),
            processTerminationService: processTerminationService ?? new RecordingDaemonProcessTerminationService(),
            artifactCleaner: artifactCleaner ?? new RecordingDaemonArtifactCleaner(),
            timeProvider: timeProvider);
    }
}
