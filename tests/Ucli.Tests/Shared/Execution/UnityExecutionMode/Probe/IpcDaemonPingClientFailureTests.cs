using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Execution.Mode.IpcDaemonPingClientTestSupport;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonPingClientFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenResponseReportsFailure_ThrowsDaemonPingResponseException ()
    {
        var unityIpcClient = new RecordingIpcTransportClient(request => CreateResponse(
            request,
            IpcResponseStatus.Error,
            [
                new IpcError(
                    Code: UcliCoreErrorCodes.InvalidArgument,
                    Message: "invalid request",
                    OpId: null),
            ]));
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(CreateResolvedSessionStore("resolved-token")),
            TimeProvider.System);

        await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    cancellationToken: CancellationToken.None).AsTask(),
                "ping failure response",
                AsyncWaitTimeout);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenSessionIsNotAvailable_ThrowsDaemonSessionNotAvailableException ()
    {
        var unityIpcClient = new UnexpectedIpcTransportClient("Missing daemon session must stop before sending IPC requests.");
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing())),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<DaemonSessionNotAvailableException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    cancellationToken: CancellationToken.None).AsTask(),
                "Missing session token ping result",
                AsyncWaitTimeout);
        });

        Assert.Equal(DaemonSessionAcquisitionResult.SessionNotAvailableMessage, exception.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenReplacementPublicationWindowFindsNoSession_PreservesReachableDaemonResponseFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        unityIpcClient.EnqueueResponse(request => CreateResponse(
            request,
            IpcResponseStatus.Error,
            [
                new IpcError(
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "The session token was replaced.",
                    OpId: null),
            ]));
        var rejectedSession = DaemonSessionTestFactory.CreateForToken("first-token");
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count == 1
                ? DaemonSessionReadResultTestFactory.Found(rejectedSession)
                : DaemonSessionReadResult.Missing(),
        };
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore),
            timeProvider);

        var pingTask = pingClient.PingAsync(
                CreateFingerprintMatchedProject(),
                DefaultTimeout,
                cancellationToken: CancellationToken.None)
            .AsTask();
        await TestAwaiter.WaitAsync(
            ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
                    timeProvider,
                    pingTask,
                    DaemonTimeouts.SessionPublicationRetryTimeout,
                    TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds))
                .AsTask(),
            "daemon ping publication window manual time",
            AsyncWaitTimeout);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(() => pingTask);

        Assert.Equal(IpcSessionErrorCodes.SessionTokenInvalid, exception.ErrorCode);
        Assert.Single(unityIpcClient.Requests);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout,
            timeProvider.GetUtcNow());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenResponseInterruptionOutlivesEndpointWindow_PreservesInterruptionFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var interruption = new IpcResponseReadInterruptedException(
            new IOException("The daemon closed the response stream."));
        var unityIpcClient = new RecordingIpcTransportClient(_ => throw interruption);
        var session = DaemonSessionTestFactory.CreateForToken("resolved-token");
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count == 1
                ? DaemonSessionReadResultTestFactory.Found(session)
                : DaemonSessionReadResult.Missing(),
        };
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore),
            timeProvider);

        var pingTask = pingClient.PingAsync(
                CreateFingerprintMatchedProject(),
                DefaultTimeout,
                cancellationToken: CancellationToken.None)
            .AsTask();
        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await TestAwaiter.WaitAsync(
            ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
                    timeProvider,
                    pingTask,
                    DaemonTimeouts.ProbeAttemptTimeoutCap,
                    retryDelay)
                .AsTask(),
            "daemon ping endpoint window manual time",
            AsyncWaitTimeout);

        var exception = await Assert.ThrowsAsync<IpcResponseReadInterruptedException>(() => pingTask);

        Assert.Same(interruption, exception);
        Assert.Single(unityIpcClient.Requests);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + DaemonTimeouts.ProbeAttemptTimeoutCap,
            timeProvider.GetUtcNow());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)ExecutionErrorKind.InvalidArgument)]
    [InlineData((int)ExecutionErrorKind.InternalError)]
    public async Task Ping_WhenSessionConnectionResolutionFailsForLocalError_ThrowsDaemonPingResponseExceptionWithoutTokenErrorCode (int errorKind)
    {
        var unityIpcClient = new UnexpectedIpcTransportClient("Failed daemon session resolution must stop before sending IPC requests.");
        var readFailure = DaemonSessionReadResult.Failure(
            new ExecutionError((ExecutionErrorKind)errorKind, "session token read failed"),
            errorKind == (int)ExecutionErrorKind.InvalidArgument
                ? DaemonSessionReadFailureKind.PathInvalid
                : DaemonSessionReadFailureKind.InternalFailure);
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(readFailure)),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<DaemonPingResponseException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    cancellationToken: CancellationToken.None).AsTask(),
                "Session token resolution failure ping result",
                AsyncWaitTimeout);
        });

        Assert.Null(exception.ErrorCode);
        Assert.Contains("session token read failed", exception.Message, StringComparison.Ordinal);
    }
}
