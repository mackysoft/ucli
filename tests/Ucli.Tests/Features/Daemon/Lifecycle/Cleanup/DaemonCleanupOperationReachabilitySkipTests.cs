using System.Net.Sockets;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonCleanupOperationReachabilitySkipTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionDoesNotExistAndEndpointResponds_ReturnsSkippedRunningWithoutCleanup ()
    {
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var daemonPingClient = DaemonCleanupOperationTestSupport.CreateSuccessfulPingClient();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Missing(),
            },
            daemonPingClient: daemonPingClient,
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-none-live"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.Running);
        Assert.Null(Assert.Single(daemonPingClient.SessionTokens));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingSucceeds_ReturnsSkippedRunningWithoutCleanup ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 2001);
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateSuccessfulPingClient(),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-running"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.Running);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsSessionTokenInvalid_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 2006);
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateFailingPingClient(
                new DaemonPingResponseException("token invalid", IpcSessionErrorCodes.SessionTokenInvalid)),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-token-invalid"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.UncertainReachability);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsSessionTokenRequired_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 2007);
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateFailingPingClient(
                new DaemonPingResponseException("token required", IpcSessionErrorCodes.SessionTokenRequired)),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-token-required"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.UncertainReachability);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsSocketAccessDenied_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 2007);
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateFailingPingClient(
                new SocketException((int)SocketError.AccessDenied)),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-access-denied"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.UncertainReachability);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsAddressNotAvailable_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 2010);
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateFailingPingClient(
                new SocketException((int)SocketError.AddressNotAvailable)),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-address-not-available"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.UncertainReachability);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionPingReturnsConnectTimeout_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 2008);
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateFailingPingClient(
                new IpcConnectTimeoutException("connect timeout")),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-connect-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.UncertainReachability);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenNamedPipeConnectTimeoutAndTrustedSessionProcessIsNotRunning_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var session = DaemonSessionTestFactory.Create(processId: 2012);
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateFailingPingClient(
                new IpcConnectTimeoutException("connect timeout")),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-connect-timeout-dead-process"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.UncertainReachability);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenSessionDoesNotExistAndProbeReturnsAddressNotAvailable_ReturnsSkippedUncertainReachabilityWithoutCleanup ()
    {
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResult.Missing(),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateFailingPingClient(
                new SocketException((int)SocketError.AddressNotAvailable)),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-none-address-not-available"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.UncertainReachability);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenPingTimesOut_ReturnsSkippedUncertainReachability ()
    {
        var artifactCleaner = new RecordingDaemonArtifactCleaner();
        var operation = DaemonCleanupOperationTestSupport.CreateOperation(
            new ManualTimeProvider(),
            daemonSessionStore: new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(DaemonSessionTestFactory.Create(processId: 2005)),
            },
            daemonPingClient: DaemonCleanupOperationTestSupport.CreateFailingPingClient(
                new TimeoutException("probe timeout")),
            artifactCleaner: artifactCleaner);

        var result = await operation.CleanupAsync(ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-cleanup-timeout"), TimeSpan.FromMilliseconds(500), CancellationToken.None);

        DaemonCleanupOperationAssert.SkippedWithoutArtifactCleanup(
            result,
            artifactCleaner,
            DaemonCleanupSkipReason.UncertainReachability);
    }
}
