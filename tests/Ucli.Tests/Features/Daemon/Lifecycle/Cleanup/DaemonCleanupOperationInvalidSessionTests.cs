using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
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
        var invalidSession = DaemonSessionTestFactory.Create(processId: 2003) with
        {
            OwnerProcessId = null,
            ProjectFingerprint = context.ProjectFingerprint,
        };
        var artifactIdentity = DaemonSessionArtifactIdentity.Create("{ invalid session artifact");
        var artifactCleaner = new RecordingDaemonArtifactCleaner
        {
            NextResult = DaemonArtifactCleanupResult.Success(),
        };
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Failure(
                    ExecutionError.InvalidArgument("invalid session"),
                    DaemonSessionReadFailureKind.InvalidSession,
                    invalidSession,
                    artifactIdentity),
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
                ReadResult = DaemonSessionReadResult.Failure(
                    ExecutionError.InvalidArgument("invalid session"),
                    DaemonSessionReadFailureKind.InvalidSession,
                    session: null,
                    artifactIdentity: null),
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
                ReadResult = DaemonSessionReadResult.Failure(
                    ExecutionError.InvalidArgument("invalid session"),
                    DaemonSessionReadFailureKind.InvalidSession,
                    session: null,
                    artifactIdentity: null),
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
        var invalidSession = DaemonSessionTestFactory.Create(processId: 2004) with
        {
            OwnerProcessId = null,
            ProjectFingerprint = context.ProjectFingerprint,
        };
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Failure(
                    ExecutionError.InvalidArgument("invalid session"),
                    DaemonSessionReadFailureKind.InvalidSession,
                    invalidSession,
                    artifactIdentity: null),
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
