using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Execution.Mode.IpcDaemonPingClientTestSupport;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class IpcDaemonPingClientRequestTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_SendsPingRequestWithProbeContract ()
    {
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            CreateResolvedSessionProvider(),
            TimeProvider.System);

        await pingClient.PingAsync(CreateFingerprintMatchedProject(), DefaultTimeout, cancellationToken: CancellationToken.None);

        var request = DaemonIpcDispatchAssert.SingleDispatchSentToEndpoint(
            unityIpcClient,
            expectedEndpointAddress: "/tmp/ucli-session.sock",
            expectedMethod: UnityIpcMethod.Ping,
            expectedSessionToken: "resolved-token");
        Assert.InRange(
            Assert.Single(unityIpcClient.Timeouts),
            TimeSpan.FromMilliseconds(1),
            DefaultTimeout);
        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.NotEqual(Guid.Empty, request.RequestId);
        Assert.Equal("ucli-mode-probe", request.Payload.GetProperty("clientVersion").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Ping_WhenCanceled_ThrowsOperationCanceledException ()
    {
        var unityIpcClient = new UnexpectedIpcTransportClient("Canceled ping must stop before sending IPC requests.");
        var sessionConnectionProvider = new UnexpectedDaemonSessionConnectionProvider("Canceled ping must stop before resolving daemon session.");
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            sessionConnectionProvider,
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
    public async Task PingCanonicalEndpointWithToken_UsesProvidedTokenWithoutResolvingSession ()
    {
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        var sessionConnectionProvider = new UnexpectedDaemonSessionConnectionProvider(
            "Canonical endpoint probing must not require readable session metadata.");
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            sessionConnectionProvider,
            TimeProvider.System);
        var unityProject = CreateFingerprintMatchedProject();

        await pingClient.PingCanonicalEndpointWithTokenAsync(
            unityProject,
            DefaultTimeout,
            "provided-token",
            CancellationToken.None);

        var expectedEndpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);
        DaemonIpcDispatchAssert.SingleDispatchSentToEndpoint(
            unityIpcClient,
            expectedEndpointAddress: expectedEndpoint.Address,
            expectedMethod: UnityIpcMethod.Ping,
            expectedSessionToken: "provided-token");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenCurrentSessionTokenRotates_ReresolvesAndRetriesOnce ()
    {
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        unityIpcClient.EnqueueResponse(request => CreateResponse(
            request,
            IpcProtocol.StatusError,
            [
                new IpcError(
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "The session token was replaced.",
                    OpId: null),
            ]));
        var firstConnection = new DaemonSessionConnection(
            "first-token",
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-first-session.sock"));
        var refreshedConnection = new DaemonSessionConnection(
            "refreshed-token",
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-refreshed-session.sock"));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            DaemonSessionConnectionResolutionResult.Success(firstConnection),
            DaemonSessionConnectionResolutionResult.Success(refreshedConnection));
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            sessionConnectionProvider,
            TimeProvider.System);

        _ = await pingClient.PingAndReadAsync(
            CreateFingerprintMatchedProject(),
            DefaultTimeout,
            validateProjectFingerprint: true,
            CancellationToken.None);

        Assert.Collection(
            unityIpcClient.Requests,
            request => Assert.Equal("first-token", request.SessionToken),
            request => Assert.Equal("refreshed-token", request.SessionToken));
        var requestId = unityIpcClient.Requests[0].RequestId;
        Assert.NotEqual(Guid.Empty, requestId);
        Assert.Equal(requestId, unityIpcClient.Requests[1].RequestId);
        Assert.Collection(
            unityIpcClient.Endpoints,
            endpoint => Assert.Equal(firstConnection.Endpoint, endpoint),
            endpoint => Assert.Equal(refreshedConnection.Endpoint, endpoint));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenInitialSessionResolutionIgnoresCancellation_TimesOutWithoutWaitingForResolution ()
    {
        var timeProvider = new ManualTimeProvider();
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        var sessionConnectionProvider = new NonCooperativeBlockingDaemonSessionConnectionProvider(
            blockOnCall: 1,
            CreateConnectionResult("initial-token", "/tmp/ucli-initial-session.sock"));
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            sessionConnectionProvider,
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
            await sessionConnectionProvider.Blocked.WaitAsync(TimeSpan.FromSeconds(1));
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
            sessionConnectionProvider.Release();
            await ObserveCompletionAsync(pingTask);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingAndRead_WhenRefreshedSessionResolutionIgnoresCancellation_TimesOutWithoutWaitingForResolution ()
    {
        var timeProvider = new ManualTimeProvider();
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        unityIpcClient.EnqueueResponse(request => CreateResponse(
            request,
            IpcProtocol.StatusError,
            [
                new IpcError(
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "The session token was replaced.",
                    OpId: null),
            ]));
        var sessionConnectionProvider = new NonCooperativeBlockingDaemonSessionConnectionProvider(
            blockOnCall: 2,
            CreateConnectionResult("first-token", "/tmp/ucli-first-session.sock"),
            CreateConnectionResult("refreshed-token", "/tmp/ucli-refreshed-session.sock"));
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            sessionConnectionProvider,
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
            await sessionConnectionProvider.Blocked.WaitAsync(TimeSpan.FromSeconds(1));
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));

            timeProvider.Advance(timeout);

            var completedTask = await Task.WhenAny(pingTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(pingTask, completedTask);
            await Assert.ThrowsAsync<TimeoutException>(() => pingTask);
            var request = Assert.Single(unityIpcClient.Requests);
            Assert.Equal("first-token", request.SessionToken);
        }
        finally
        {
            sessionConnectionProvider.Release();
            await ObserveCompletionAsync(pingTask);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task PingSessionAndRead_UsesEndpointAndTokenFromSameSessionSnapshot ()
    {
        var unityIpcClient = CreateSuccessfulPingTransportClient();
        var sessionConnectionProvider = new UnexpectedDaemonSessionConnectionProvider(
            "A captured session probe must not resolve a newer session generation.");
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            sessionConnectionProvider,
            TimeProvider.System);
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "captured-token",
            endpointTransportKind: "unixDomainSocket",
            endpointAddress: "/tmp/ucli-captured-session.sock");

        _ = await pingClient.PingSessionAndReadAsync(
            CreateFingerprintMatchedProject(),
            session,
            DefaultTimeout,
            validateProjectFingerprint: true,
            CancellationToken.None);

        DaemonIpcDispatchAssert.SingleDispatchSentToEndpoint(
            unityIpcClient,
            expectedEndpointAddress: "/tmp/ucli-captured-session.sock",
            expectedMethod: UnityIpcMethod.Ping,
            expectedSessionToken: "captured-token");
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Ping_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException (int timeoutMilliseconds)
    {
        var unityIpcClient = new UnexpectedIpcTransportClient("Invalid timeout must stop before sending IPC requests.");
        var sessionConnectionProvider = new UnexpectedDaemonSessionConnectionProvider("Invalid timeout must stop before resolving daemon session.");
        var pingClient = new IpcDaemonPingClient(
            unityIpcClient,
            sessionConnectionProvider,
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

    private static DaemonSessionConnectionResolutionResult CreateConnectionResult (
        string sessionToken,
        string endpointAddress)
    {
        return DaemonSessionConnectionResolutionResult.Success(new DaemonSessionConnection(
            sessionToken,
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, endpointAddress)));
    }

    private static async Task ObserveCompletionAsync (Task task)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(1)));
        if (ReferenceEquals(completedTask, task))
        {
            _ = task.Exception;
        }
    }

    private sealed class NonCooperativeBlockingDaemonSessionConnectionProvider : IDaemonSessionConnectionProvider
    {
        private readonly int blockOnCall;

        private readonly Queue<DaemonSessionConnectionResolutionResult> results;

        private readonly TaskCompletionSource<bool> blockedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> releaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int callCount;

        public NonCooperativeBlockingDaemonSessionConnectionProvider (
            int blockOnCall,
            params DaemonSessionConnectionResolutionResult[] results)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(blockOnCall, 1);
            ArgumentNullException.ThrowIfNull(results);
            if (results.Length < blockOnCall)
            {
                throw new ArgumentException("A result is required for every call through the blocking call.", nameof(results));
            }

            this.blockOnCall = blockOnCall;
            this.results = new Queue<DaemonSessionConnectionResolutionResult>(results);
        }

        public Task Blocked => blockedSource.Task;

        public ValueTask<DaemonSessionConnectionResolutionResult> ResolveAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
            cancellationToken.ThrowIfCancellationRequested();
            var result = results.Dequeue();
            var currentCall = Interlocked.Increment(ref callCount);
            if (currentCall != blockOnCall)
            {
                return ValueTask.FromResult(result);
            }

            return new ValueTask<DaemonSessionConnectionResolutionResult>(WaitForReleaseAsync(result));
        }

        public void Release ()
        {
            releaseSource.TrySetResult(true);
        }

        private async Task<DaemonSessionConnectionResolutionResult> WaitForReleaseAsync (
            DaemonSessionConnectionResolutionResult result)
        {
            blockedSource.TrySetResult(true);
            await releaseSource.Task;
            return result;
        }
    }
}
