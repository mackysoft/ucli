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
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                CreateSessionReadResult("daemon-token-1"),
                CreateSessionReadResult("daemon-token-1"),
                CreateSessionReadResult("daemon-token-2"),
                CreateSessionReadResult("daemon-token-3"))));

        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-token-publication")),
                CreateSession("daemon-token-1"),
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
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(CreateSessionReadResult("daemon-token-1"))));

        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-token-not-rotated")),
                CreateSession("daemon-token-1"),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        await timeProvider
            .WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds))
            .WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(DaemonTimeouts.SessionPublicationRetryTimeout);

        var result = await resultTask.WaitAsync(TimeSpan.FromSeconds(1));

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
    public async Task SendShutdown_WhenReplacementSessionResolutionIgnoresCancellation_ReturnsRejectionAtPublicationDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(
            static request => IpcResponseTestFactory.CreateError(
                request,
                IpcSessionErrorCodes.SessionTokenInvalid,
                "session rejected"));
        var sessionStore = new NonCooperativeBlockingDaemonSessionStore(
            blockOnCall: 1,
            CreateSessionReadResult("daemon-token-2"));
        var client = new DaemonShutdownClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-blocked-publication")),
                CreateSession("daemon-token-1"),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        await sessionStore.Blocked.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            await timeProvider
                .WaitForTimerDueWithinAsync(DaemonTimeouts.SessionPublicationRetryTimeout)
                .WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(DaemonTimeouts.SessionPublicationRetryTimeout);
            var completedTask = await Task.WhenAny(resultTask, Task.Delay(TimeSpan.FromSeconds(1)));

            Assert.Same(resultTask, completedTask);
            var result = await resultTask;
            Assert.False(result.IsSuccess);
            Assert.False(result.IsNotRunning);
            var error = Assert.IsType<ExecutionError>(result.Error);
            Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
            Assert.Contains(IpcSessionErrorCodes.SessionTokenInvalid.Value, error.Message, StringComparison.Ordinal);
            Assert.Single(transportClient.Requests);
            Assert.Equal(
                DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout,
                timeProvider.GetUtcNow());
        }
        finally
        {
            sessionStore.Release();
            await ObserveCompletionAsync(resultTask);
        }
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
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(CreateSessionReadResult("daemon-token-1"))));
        var timeout = TimeSpan.FromMilliseconds(500);

        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-token-deadline")),
                CreateSession("daemon-token-1"),
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenSuccessorSessionTokenIsRejected_FollowsNextPublishedGeneration ()
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
                2 => IpcResponseTestFactory.CreateError(
                    request,
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "successor session rejected"),
                _ => IpcResponseTestFactory.CreateSuccess(
                    request,
                    new IpcShutdownResponse(Accepted: true, Message: "shutdown accepted")),
            };
        });
        var client = new DaemonShutdownClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                CreateSessionReadResult("daemon-token-2"),
                CreateSessionReadResult("daemon-token-3"))));

        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-successor-terminal")),
                CreateSession("daemon-token-1"),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await resultTask;

        Assert.True(result.IsSuccess);
        var requests = transportClient.Requests;
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-3").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenSuccessorResponseIsInterrupted_DoesNotReplayAmbiguousRequest ()
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
                _ => throw new IpcResponseReadInterruptedException(
                    new IOException("shutdown response was lost")),
            };
        });
        var client = new DaemonShutdownClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                CreateSessionReadResult("daemon-token-2"),
                CreateSessionReadResult("daemon-token-3"))));

        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-interrupted-successor")),
                CreateSession("daemon-token-1"),
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
    public async Task SendShutdown_WhenSuccessorEndpointFailsBeforeWrite_FollowsNextPublishedGeneration ()
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
                2 => throw new IpcConnectTimeoutException(
                    "The successor endpoint timed out before the shutdown request was sent."),
                _ => IpcResponseTestFactory.CreateSuccess(
                    request,
                    new IpcShutdownResponse(Accepted: true, Message: "shutdown accepted")),
            };
        });
        var client = new DaemonShutdownClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                CreateSessionReadResult("daemon-token-2"),
                CreateSessionReadResult("daemon-token-3"))));

        var resultTask = client.SendShutdownAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(
                    ProjectFingerprintTestFactory.Create("fingerprint-shutdown-successor-connect")),
                CreateSession("daemon-token-1"),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await resultTask;

        Assert.True(result.IsSuccess);
        IpcRequestAssert.SessionTokens(
            transportClient.Requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-3").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(transportClient.Requests);
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
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new UnexpectedDaemonSessionStore("A pending shutdown response must not resolve another daemon session.")));
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
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new UnexpectedDaemonSessionStore("A shutdown response timeout must not resolve another daemon session.")));

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
    public async Task SendShutdown_WhenInitialEndpointRefusesConnection_FollowsPublishedSuccessor ()
    {
        var startedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(startedAtUtc);
        var transportClient = new RecordingIpcTransportClient(
            static request => IpcResponseTestFactory.CreateSuccess(
                request,
                new IpcShutdownResponse(Accepted: true, Message: "shutdown accepted")));
        transportClient.EnqueueException(new IpcConnectException(
            "IPC connection was refused before the request was sent.",
            new SocketException((int)SocketError.ConnectionRefused)));
        var initialSession = DaemonSessionTestFactory.CreateForToken(
            "initial-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-initial-endpoint");
        var successorSession = DaemonSessionTestFactory.CreateForToken(
            "successor-token",
            endpointTransportKind: IpcTransportKind.NamedPipe,
            endpointAddress: "ucli-successor-endpoint");
        var client = new DaemonShutdownClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(successorSession))));

        var result = await client.SendShutdownAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-shutdown-not-running")),
            initialSession,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsNotRunning);
        Assert.Null(result.Error);
        IpcRequestAssert.SessionTokens(
            transportClient.Requests,
            initialSession.SessionToken.GetEncodedValue(),
            successorSession.SessionToken.GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(transportClient.Requests);
        Assert.Collection(
            transportClient.Endpoints,
            endpoint => Assert.Equal(initialSession.Endpoint, endpoint),
            endpoint => Assert.Equal(successorSession.Endpoint, endpoint));
        Assert.All(
            transportClient.Requests,
            request => Assert.Equal(startedAtUtc + TimeSpan.FromSeconds(5), request.RequestDeadlineUtc));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenSocketConnectionReset_ReturnsFailure ()
    {
        var transportClient = new RecordingIpcTransportClient(static _ => throw new InvalidOperationException("unexpected transport response"));
        transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionReset));
        var client = new DaemonShutdownClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new UnexpectedDaemonSessionStore("A lost shutdown response must not resolve another daemon session.")));

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
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new UnexpectedDaemonSessionStore("A missing shutdown token is not a session-publication race.")));

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

    private static DaemonSessionReadResult CreateSessionReadResult (string sessionToken)
    {
        return DaemonSessionReadResultTestFactory.FoundForToken(
            sessionToken,
            IpcTransportKind.NamedPipe,
            "ucli-daemon-test-endpoint");
    }

    private static DaemonSession CreateSession (string sessionToken)
    {
        return DaemonSessionTestFactory.CreateForToken(
            sessionToken,
            IpcTransportKind.NamedPipe,
            "ucli-daemon-test-endpoint");
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
