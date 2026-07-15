using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Execution.Mode.IpcDaemonPingClientTestSupport;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonPingClientRequestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_SendsPingRequestWithProbeContract ()
    {
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(CreateResolvedSessionStore("resolved-token")),
            timeProvider);

        await pingClient.PingAsync(CreateFingerprintMatchedProject(), DefaultTimeout, cancellationToken: CancellationToken.None);

        var request = DaemonIpcDispatchAssert.SingleDispatchSentToEndpoint(
            unityIpcClient,
            expectedEndpointAddress: "/tmp/ucli-session.sock",
            expectedMethod: UnityIpcMethod.Ping,
            expectedSessionToken: IpcSessionTokenTestFactory.Create("resolved-token").GetEncodedValue());
        var transportTimeout = Assert.Single(unityIpcClient.Timeouts);
        Assert.InRange(transportTimeout, TimeSpan.FromMilliseconds(1), DefaultTimeout);
        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.NotEqual(Guid.Empty, request.RequestId);
        Assert.Equal(startedAtUtc + DefaultTimeout, request.RequestDeadlineUtc);
        Assert.Equal("ucli-mode-probe", request.Payload.GetProperty("clientVersion").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenCanceled_ThrowsOperationCanceledException ()
    {
        var unityIpcClient = new UnexpectedIpcTransportClient("Canceled ping must stop before sending IPC requests.");
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(CreateUnexpectedSessionStore(
                "Canceled ping must stop before resolving daemon session.")),
            TimeProvider.System);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAsync(
                    CreateFingerprintMatchedProject(),
                    DefaultTimeout,
                    cancellationToken: cancellationTokenSource.Token).AsTask(),
                "Canceled daemon ping",
                AsyncWaitTimeout);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingCanonicalEndpointWithSessionToken_UsesProvidedTokenWithoutResolvingSession ()
    {
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(CreateUnexpectedSessionStore(
                "Canonical endpoint probing must not require readable session metadata.")),
            TimeProvider.System);
        var unityProject = CreateFingerprintMatchedProject();

        var sessionToken = IpcSessionTokenTestFactory.CreateFromDiscriminator(1);
        await pingClient.PingCanonicalEndpointWithSessionTokenAsync(
            unityProject,
            DefaultTimeout,
            sessionToken,
            CancellationToken.None);

        var expectedEndpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        DaemonIpcDispatchAssert.SingleDispatchSentToEndpoint(
            unityIpcClient,
            expectedEndpointAddress: expectedEndpoint.Address,
            expectedMethod: UnityIpcMethod.Ping,
            expectedSessionToken: sessionToken.GetEncodedValue());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenReplacementPublicationIsDelayed_RetriesOnceWithSameRequestId ()
    {
        var unityProject = CreateFingerprintMatchedProject();
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
        var rejectedSession = DaemonSessionTestFactory.CreateForToken(
            "first-token",
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli-first-session.sock");
        var replacementSession = DaemonSessionTestFactory.CreateForToken(
            "refreshed-token",
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli-refreshed-session.sock");
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count switch
            {
                1 => DaemonSessionReadResultTestFactory.Found(rejectedSession),
                2 => DaemonSessionReadResult.Missing(),
                _ => DaemonSessionReadResultTestFactory.Found(replacementSession),
            },
        };
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore),
            timeProvider);

        var pingTask = pingClient.PingAndReadAsync(
                unityProject,
                DefaultTimeout,
                validateProjectFingerprint: true,
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            pingTask,
            DefaultTimeout,
            TimeSpan.FromMilliseconds(100));

        _ = await pingTask;

        Assert.Equal(3, sessionStore.ReadInvocations.Count);
        Assert.Collection(
            unityIpcClient.Requests,
            request => Assert.Equal(
                IpcSessionTokenTestFactory.Create("first-token").GetEncodedValue(),
                request.SessionToken),
            request => Assert.Equal(
                IpcSessionTokenTestFactory.Create("refreshed-token").GetEncodedValue(),
                request.SessionToken));
        var requestId = unityIpcClient.Requests[0].RequestId;
        Assert.NotEqual(Guid.Empty, requestId);
        Assert.Equal(requestId, unityIpcClient.Requests[1].RequestId);
        Assert.Collection(
            unityIpcClient.Endpoints,
            endpoint => Assert.Equal(rejectedSession.Endpoint, endpoint),
            endpoint => Assert.Equal(replacementSession.Endpoint, endpoint));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenSuccessorSessionTokenIsRejected_FollowsNextPublishedGeneration ()
    {
        var unityProject = CreateFingerprintMatchedProject();
        var timeProvider = new ManualTimeProvider();
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        unityIpcClient.EnqueueResponse(request => CreateResponse(
            request,
            IpcResponseStatus.Error,
            [
                new IpcError(
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "The initial session token was replaced.",
                    OpId: null),
            ]));
        unityIpcClient.EnqueueResponse(request => CreateResponse(
            request,
            IpcResponseStatus.Error,
            [
                new IpcError(
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "The successor session token was replaced.",
                    OpId: null),
            ]));
        var initialSession = DaemonSessionTestFactory.CreateForToken(
            "initial-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-initial-session");
        var successorSession = DaemonSessionTestFactory.CreateForToken(
            "successor-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-successor-session");
        var latestSession = DaemonSessionTestFactory.CreateForToken(
            "latest-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-latest-session");
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count switch
            {
                1 => DaemonSessionReadResultTestFactory.Found(initialSession),
                2 => DaemonSessionReadResultTestFactory.Found(successorSession),
                _ => DaemonSessionReadResultTestFactory.Found(latestSession),
            },
        };
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore),
            timeProvider);

        _ = await pingClient.PingAndReadAsync(
            unityProject,
            DefaultTimeout,
            validateProjectFingerprint: true,
            CancellationToken.None);

        IpcRequestAssert.SessionTokens(
            unityIpcClient.Requests,
            initialSession.SessionToken.GetEncodedValue(),
            successorSession.SessionToken.GetEncodedValue(),
            latestSession.SessionToken.GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(unityIpcClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenSuccessorConnectionFailsBeforeWrite_FollowsNextPublishedGeneration ()
    {
        var unityProject = CreateFingerprintMatchedProject();
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        unityIpcClient.EnqueueResponse(request => CreateResponse(
            request,
            IpcResponseStatus.Error,
            [
                new IpcError(
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "The initial session token was replaced.",
                    OpId: null),
            ]));
        unityIpcClient.EnqueueResponse(static _ => throw new IpcConnectTimeoutException(
            "The successor endpoint timed out before the ping request was sent."));
        var initialSession = DaemonSessionTestFactory.CreateForToken(
            "initial-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-initial-session");
        var successorSession = DaemonSessionTestFactory.CreateForToken(
            "successor-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-successor-session");
        var latestSession = DaemonSessionTestFactory.CreateForToken(
            "latest-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-latest-session");
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count switch
            {
                1 => DaemonSessionReadResultTestFactory.Found(initialSession),
                2 => DaemonSessionReadResultTestFactory.Found(successorSession),
                _ => DaemonSessionReadResultTestFactory.Found(latestSession),
            },
        };
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore),
            TimeProvider.System);

        _ = await pingClient.PingAndReadAsync(
            unityProject,
            DefaultTimeout,
            validateProjectFingerprint: true,
            CancellationToken.None);

        IpcRequestAssert.SessionTokens(
            unityIpcClient.Requests,
            initialSession.SessionToken.GetEncodedValue(),
            successorSession.SessionToken.GetEncodedValue(),
            latestSession.SessionToken.GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(unityIpcClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenReplacementPublicationExhaustsRequestDeadline_ThrowsTimeoutException ()
    {
        var unityProject = CreateFingerprintMatchedProject();
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
        var rejectedSession = DaemonSessionTestFactory.CreateForToken(
            "first-token",
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli-first-session.sock");
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(rejectedSession))),
            timeProvider);
        var timeout = TimeSpan.FromMilliseconds(150);

        var pingTask = pingClient.PingAndReadAsync(
                unityProject,
                timeout,
                validateProjectFingerprint: true,
                CancellationToken.None)
            .AsTask();
        await AdvanceNextPublicationRetryAsync(timeProvider);
        await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(50));
        timeProvider.Advance(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TimeoutException>(() => pingTask);
        Assert.Single(unityIpcClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenCanceledDuringReplacementPublicationWait_StopsWithoutResend ()
    {
        var unityProject = CreateFingerprintMatchedProject();
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
        var rejectedSession = DaemonSessionTestFactory.CreateForToken(
            "first-token",
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli-first-session.sock");
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(rejectedSession))),
            timeProvider);
        using var cancellationTokenSource = new CancellationTokenSource();

        var pingTask = pingClient.PingAndReadAsync(
                unityProject,
                DefaultTimeout,
                validateProjectFingerprint: true,
                cancellationTokenSource.Token)
            .AsTask();
        await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(100));

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pingTask);
        Assert.Single(unityIpcClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenInitialSessionResolutionIgnoresCancellation_TimesOutWithoutWaitingForResolution ()
    {
        var timeProvider = new ManualTimeProvider();
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        var sessionStore = new NonCooperativeBlockingDaemonSessionStore(
            blockOnCall: 1,
            DaemonSessionReadResultTestFactory.FoundForToken(
                "initial-token",
                endpointAddress: "/tmp/ucli-initial-session.sock"));
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore),
            timeProvider);
        var timeout = TimeSpan.FromSeconds(5);
        var pingTask = pingClient.PingAndReadAsync(
                CreateFingerprintMatchedProject(),
                timeout,
                validateProjectFingerprint: true,
                CancellationToken.None)
            .AsTask();

        try
        {
            await sessionStore.Blocked.WaitAsync(TimeSpan.FromSeconds(1));
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));

            timeProvider.Advance(timeout);

            var completedTask = await Task.WhenAny(pingTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(pingTask, completedTask);
            await Assert.ThrowsAsync<TimeoutException>(() => pingTask);
            Assert.Empty(unityIpcClient.Requests);
        }
        finally
        {
            sessionStore.Release();
            await ObserveCompletionAsync(pingTask);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenReplacementSessionReadIgnoresCancellation_ThrowsTimeoutWithoutWaitingForRead ()
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
        var rejectedSession = DaemonSessionTestFactory.CreateForToken(
            "first-token",
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli-first-session.sock");
        var sessionStore = new NonCooperativeBlockingDaemonSessionStore(
            blockOnCall: 2,
            DaemonSessionReadResultTestFactory.Found(rejectedSession),
            DaemonSessionReadResult.Missing());
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore),
            timeProvider);
        var timeout = TimeSpan.FromSeconds(5);
        var pingTask = pingClient.PingAndReadAsync(
                CreateFingerprintMatchedProject(),
                timeout,
                validateProjectFingerprint: true,
                CancellationToken.None)
            .AsTask();

        try
        {
            await sessionStore.Blocked.WaitAsync(TimeSpan.FromSeconds(1));
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));

            timeProvider.Advance(timeout);

            var completedTask = await Task.WhenAny(pingTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(pingTask, completedTask);
            await Assert.ThrowsAsync<TimeoutException>(() => pingTask);
            var request = Assert.Single(unityIpcClient.Requests);
            Assert.Equal(
                IpcSessionTokenTestFactory.Create("first-token").GetEncodedValue(),
                request.SessionToken);
        }
        finally
        {
            sessionStore.Release();
            await ObserveCompletionAsync(pingTask);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingSessionAndRead_UsesEndpointAndTokenFromSameSessionSnapshot ()
    {
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(CreateUnexpectedSessionStore(
                "A captured session probe must not resolve a newer session generation.")),
            TimeProvider.System);
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "captured-token",
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli-captured-session.sock");
        var requestId = Guid.NewGuid();
        var deadline = ExecutionDeadline.Start(DefaultTimeout, TimeProvider.System);

        _ = await pingClient.PingSessionAndReadAsync(
            CreateFingerprintMatchedProject(),
            session,
            requestId,
            deadline,
            validateProjectFingerprint: true,
            CancellationToken.None);

        var request = DaemonIpcDispatchAssert.SingleDispatchSentToEndpoint(
            unityIpcClient,
            expectedEndpointAddress: "/tmp/ucli-captured-session.sock",
            expectedMethod: UnityIpcMethod.Ping,
            expectedSessionToken: IpcSessionTokenTestFactory.Create("captured-token").GetEncodedValue());
        Assert.Equal(requestId, request.RequestId);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Ping_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException (int timeoutMilliseconds)
    {
        var unityIpcClient = new UnexpectedIpcTransportClient("Invalid timeout must stop before sending IPC requests.");
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(CreateUnexpectedSessionStore(
                "Invalid timeout must stop before resolving daemon session.")),
            TimeProvider.System);
        var timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                pingClient.PingAsync(
                    CreateFingerprintMatchedProject(),
                    timeout,
                    cancellationToken: CancellationToken.None).AsTask(),
                "Invalid timeout ping result",
                AsyncWaitTimeout);
        });
    }

    private static async Task AdvanceNextPublicationRetryAsync (ManualTimeProvider timeProvider)
    {
        var retryDelay = TimeSpan.FromMilliseconds(100);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay);
        timeProvider.Advance(retryDelay);
    }

    private static async Task ObserveCompletionAsync (Task task)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(1)));
        if (ReferenceEquals(completedTask, task))
        {
            _ = task.Exception;
        }
    }

}
