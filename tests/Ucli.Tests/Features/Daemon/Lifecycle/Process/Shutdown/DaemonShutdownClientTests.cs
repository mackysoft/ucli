using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonShutdownClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenSessionTokenPublicationLags_RetriesOnceWithPublishedSuccessor ()
    {
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var transportClient = new RecordingIpcTransportClient(
            static request => IpcResponseTestFactory.CreateSuccess(
                request,
                new IpcShutdownResponse(Accepted: true, Message: "shutdown accepted")));
        transportClient.EnqueueResponse(static request => IpcResponseTestFactory.CreateError(
            request,
            IpcSessionErrorCodes.SessionTokenInvalid,
            "session rejected"));
        transportClient.EnqueueResponse(static request => IpcResponseTestFactory.CreateSuccess(
            request,
            new IpcShutdownResponse(Accepted: true, Message: "shutdown accepted")));
        var client = new DaemonShutdownClient(
            transportClient,
            new QueuedDaemonSessionConnectionProvider(
                CreateConnectionResult("daemon-token-1"),
                CreateConnectionResult("daemon-token-1"),
                CreateConnectionResult("daemon-token-2"),
                CreateConnectionResult("daemon-token-3")),
            recoveryWaiter: null,
            timeProvider);

        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-token-publication")),
                DaemonSessionTestFactory.Create(
                    sessionToken: "daemon-token-1",
                    endpointAddress: "ucli-daemon-test-endpoint"),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await resultTask;

        Assert.True(result.IsSuccess);
        var requests = transportClient.Requests;
        Assert.Collection(
            requests,
            static request => Assert.Equal(ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown), request.Method),
            static request => Assert.Equal(ContractLiteralCodec.ToValue(UnityIpcMethod.Shutdown), request.Method));
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.All(
            requests,
            request => Assert.Equal(startedAtUtc + TimeSpan.FromSeconds(5), request.RequestDeadlineUtc));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenRejectedSessionTokenDoesNotRotate_ReturnsFailureAfterPublicationGrace ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(
            static request => IpcResponseTestFactory.CreateError(
                request,
                IpcSessionErrorCodes.SessionTokenInvalid,
                "session rejected"));
        var client = new DaemonShutdownClient(
            transportClient,
            new StaticDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token-1")),
            recoveryWaiter: null,
            timeProvider);

        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-token-not-rotated")),
                DaemonSessionTestFactory.Create(
                    sessionToken: "daemon-token-1",
                    endpointAddress: "ucli-daemon-test-endpoint"),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await resultTask;

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotRunning);
        Assert.Equal(ExecutionErrorKind.InternalError, Assert.IsType<ExecutionError>(result.Error).Kind);
        Assert.Single(transportClient.Requests);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout,
            timeProvider.GetUtcNow());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenOverallDeadlinePrecedesPublicationGrace_ReturnsTimeoutAtOverallDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(
            static request => IpcResponseTestFactory.CreateError(
                request,
                IpcSessionErrorCodes.SessionTokenInvalid,
                "session rejected"));
        var client = new DaemonShutdownClient(
            transportClient,
            new StaticDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token-1")),
            recoveryWaiter: null,
            timeProvider);
        var timeout = TimeSpan.FromMilliseconds(500);

        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-token-deadline")),
                DaemonSessionTestFactory.Create(
                    sessionToken: "daemon-token-1",
                    endpointAddress: "ucli-daemon-test-endpoint"),
                ExecutionDeadline.Start(timeout, timeProvider),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await resultTask;

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotRunning);
        Assert.Equal(ExecutionErrorKind.Timeout, Assert.IsType<ExecutionError>(result.Error).Kind);
        Assert.Single(transportClient.Requests);
        Assert.Equal(DateTimeOffset.UnixEpoch + timeout, timeProvider.GetUtcNow());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendShutdown_WhenSuccessorAttemptDoesNotComplete_DoesNotTryThirdSession (
        bool successorTokenRejected)
    {
        var timeProvider = new ManualTimeProvider();
        var attempt = 0;
        var transportClient = new RecordingIpcTransportClient(request =>
        {
            return Interlocked.Increment(ref attempt) switch
            {
                1 => IpcResponseTestFactory.CreateError(
                    request,
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "initial session rejected"),
                2 when successorTokenRejected => IpcResponseTestFactory.CreateError(
                    request,
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "successor session rejected"),
                2 => throw new IpcResponseReadInterruptedException(
                    new IOException("shutdown response was lost")),
                _ => IpcResponseTestFactory.CreateSuccess(
                    request,
                    new IpcShutdownResponse(Accepted: true, Message: "shutdown accepted")),
            };
        });
        var client = new DaemonShutdownClient(
            transportClient,
            new QueuedDaemonSessionConnectionProvider(
                CreateConnectionResult("daemon-token-2"),
                CreateConnectionResult("daemon-token-3")),
            recoveryWaiter: null,
            timeProvider);

        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-successor-terminal")),
                DaemonSessionTestFactory.Create(
                    sessionToken: "daemon-token-1",
                    endpointAddress: "ucli-daemon-test-endpoint"),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await resultTask;

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotRunning);
        Assert.Equal(ExecutionErrorKind.InternalError, Assert.IsType<ExecutionError>(result.Error).Kind);
        var requests = transportClient.Requests;
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenTransportIgnoresCancellation_ReturnsAtSharedDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var sendStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sendCompletion = new TaskCompletionSource<IpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new StubIpcTransportClient
        {
            SendHandler = (_, _, _, _) =>
            {
                sendStarted.TrySetResult();
                return new ValueTask<IpcResponse>(sendCompletion.Task);
            },
        };
        var client = new DaemonShutdownClient(
            transportClient,
            new UnexpectedDaemonSessionConnectionProvider("A pending shutdown response must not resolve another daemon session."),
            recoveryWaiter: null,
            timeProvider);
        var timeout = TimeSpan.FromSeconds(1);

        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-non-cooperative-transport")),
                DaemonSessionTestFactory.Create(
                    sessionToken: "session-token",
                    endpointAddress: "ucli-daemon-test-endpoint"),
                ExecutionDeadline.Start(timeout, timeProvider),
                CancellationToken.None)
            .AsTask();
        await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await timeProvider.WaitForTimerDueWithinAsync(timeout).WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(timeout);
            var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.False(result.IsSuccess);
            Assert.False(result.IsNotRunning);
            Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        }
        finally
        {
            sendCompletion.TrySetException(new TimeoutException("Release non-cooperative shutdown transport."));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenIpcTimesOut_ReturnsTimeoutFailure ()
    {
        var transportClient = new RecordingIpcTransportClient(static _ => throw new InvalidOperationException("unexpected transport response"));
        transportClient.EnqueueException(new TimeoutException("ipc timeout"));
        var client = new DaemonShutdownClient(
            transportClient,
            new UnexpectedDaemonSessionConnectionProvider("A shutdown response timeout must not resolve another daemon session."),
            recoveryWaiter: null,
            TimeProvider.System);

        var result = await client.SendShutdownAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-shutdown-timeout")),
            DaemonSessionTestFactory.Create(
                sessionToken: "session-token",
                endpointAddress: "ucli-daemon-test-endpoint"),
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotRunning);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenSocketConnectionIsRefused_ReturnsNotRunning ()
    {
        var transportClient = new RecordingIpcTransportClient(static _ => throw new InvalidOperationException("unexpected transport response"));
        transportClient.EnqueueException(new IpcConnectException(
            "IPC connection was refused before the request was sent.",
            new SocketException((int)SocketError.ConnectionRefused)));
        var client = new DaemonShutdownClient(
            transportClient,
            new UnexpectedDaemonSessionConnectionProvider("A refused shutdown connection must not resolve another daemon session."),
            recoveryWaiter: null,
            TimeProvider.System);

        var result = await client.SendShutdownAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-shutdown-not-running")),
            DaemonSessionTestFactory.Create(
                sessionToken: "session-token",
                endpointAddress: "ucli-daemon-test-endpoint"),
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsNotRunning);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenSocketConnectionReset_ReturnsFailure ()
    {
        var transportClient = new RecordingIpcTransportClient(static _ => throw new InvalidOperationException("unexpected transport response"));
        transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionReset));
        var client = new DaemonShutdownClient(
            transportClient,
            new UnexpectedDaemonSessionConnectionProvider("A lost shutdown response must not resolve another daemon session."),
            recoveryWaiter: null,
            TimeProvider.System);

        var result = await client.SendShutdownAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-shutdown-transport-error")),
            DaemonSessionTestFactory.Create(
                sessionToken: "session-token",
                endpointAddress: "ucli-daemon-test-endpoint"),
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotRunning);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenSessionTokenIsRequired_ReturnsFailure ()
    {
        var transportClient = new RecordingIpcTransportClient(
            request => IpcResponseTestFactory.CreateError(
                request,
                IpcSessionErrorCodes.SessionTokenRequired,
                "session rejected"));
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var client = new DaemonShutdownClient(
            transportClient,
            new UnexpectedDaemonSessionConnectionProvider("A missing shutdown token is not a session-publication race."),
            recoveryWaiter: null,
            timeProvider);

        var result = await client.SendShutdownAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-shutdown-auth-rejected")),
            DaemonSessionTestFactory.Create(
                sessionToken: "session-token",
                endpointAddress: "ucli-daemon-test-endpoint"),
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotRunning);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains(IpcSessionErrorCodes.SessionTokenRequired.Value, error.Message, StringComparison.Ordinal);
        var endpoint = Assert.Single(transportClient.Endpoints);
        Assert.Equal(IpcTransportKind.NamedPipe, endpoint.TransportKind);
        Assert.Equal("ucli-daemon-test-endpoint", endpoint.Address);
        var request = Assert.Single(transportClient.Requests);
        Assert.Equal(startedAtUtc + TimeSpan.FromMilliseconds(500), request.RequestDeadlineUtc);
    }

    private static DaemonSessionConnectionResolutionResult CreateConnectionResult (string sessionToken)
    {
        return DaemonSessionConnectionResolutionResult.Success(new DaemonSessionConnection(
            IpcSessionTokenTestFactory.Create(sessionToken),
            new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-daemon-test-endpoint")));
    }
}
