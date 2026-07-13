using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonCleanupOperationArtifactCleanupTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionDoesNotExistAndProbeShowsNotRunning_CompletesCleanup ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-none");
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Missing(),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateNotRunningPingClient(),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.CompletedAfterArtifactCleanup(result, artifactCleaner, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenArtifactCleanerDeletesLaunchAttempts_PropagatesDeletedLaunchAttemptCount ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-deleted-attempts");
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(deletedLaunchAttemptCount: 3),
        };
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Missing(),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateNotRunningPingClient(),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.CompletedAfterArtifactCleanup(
            result,
            artifactCleaner,
            context,
            expectedDeletedLaunchAttemptCount: 3);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsNotRunningException_CompletesCleanup ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-stale");
        var session = DaemonSessionTestFactory.Create(processId: 2002);
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateNotRunningPingClient(),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.CompletedAfterArtifactCleanup(result, artifactCleaner, context);
        Assert.Equal(session, Assert.Single(artifactCleaner.Invocations).ExpectedSession);
    }
}
