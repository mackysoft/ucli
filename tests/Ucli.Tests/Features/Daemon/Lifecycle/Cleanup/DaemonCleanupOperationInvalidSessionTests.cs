using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonCleanupOperationInvalidSessionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenInvalidSessionCanBeCleaned_CompletesCleanup ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-invalid-safe");
        var invalidEvidence = DaemonInvalidSessionEvidenceTestFactory.Create(
            projectFingerprint: context.ProjectFingerprint,
            processId: 2003);
        var artifactIdentity = DaemonSessionArtifactIdentity.Create("{ invalid session artifact");
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Invalid(invalidEvidence, artifactIdentity),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateNotRunningPingClient(),
            artifactCleaner: artifactCleaner,
            invalidSessionCleanupSafetyEvaluator: new RecordingDaemonInvalidSessionCleanupSafetyEvaluator
            {
                RequiresUnsafeSkipResult = false,
            });

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.CompletedAfterArtifactCleanup(result, artifactCleaner, context);
        Assert.Equal(artifactIdentity, Assert.Single(artifactCleaner.Invocations).ExpectedArtifactIdentity);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenInvalidSessionHasNoParsedMetadataAndProbeShowsNotRunning_CompletesCleanup ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-invalid-null");
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Invalid(),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateNotRunningPingClient(),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.CompletedAfterArtifactCleanup(result, artifactCleaner, context);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenInvalidSessionHasNoParsedMetadataAndProbeReturnsConnectTimeout_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-invalid-connect-timeout");
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Invalid(),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateFailingPingClient(
                new IpcConnectTimeoutException("connect timeout")),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.UncertainReachability);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenInvalidSessionIsUnsafe_ReturnsSkippedWithoutProbing ()
    {
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-invalid-unsafe");
        var invalidEvidence = DaemonInvalidSessionEvidenceTestFactory.Create(
            projectFingerprint: context.ProjectFingerprint,
            processId: 2004);
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Invalid(invalidEvidence),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateFailingPingClient(
                new InvalidDataException("probe should not run")),
            artifactCleaner: artifactCleaner,
            invalidSessionCleanupSafetyEvaluator: new RecordingDaemonInvalidSessionCleanupSafetyEvaluator
            {
                RequiresUnsafeSkipResult = true,
            });

        var result = await operation.CleanupAsync(context, TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.UnsafeInvalidSession);
    }
}
