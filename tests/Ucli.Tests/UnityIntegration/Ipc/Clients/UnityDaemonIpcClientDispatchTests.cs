using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Acquisition;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientDispatchTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSuccessful_ReadsSessionAndDelegatesToTransport ()
    {
        var response = CreateResponse(Guid.NewGuid());
        var transportClient = new RecordingIpcTransportClient(_ => response);
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertUnityResponse(response, result.Response);
        var request = DaemonIpcDispatchAssert.SingleDispatchSentToEndpoint(
            transportClient,
            Path.GetFullPath("/tmp/ucli-session.sock"),
            UnityIpcMethod.OpsRead,
            IpcSessionTokenTestFactory.Create("daemon-token").GetEncodedValue());
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(CreateDispatchPayload().GetRawText()),
            JsonNode.Parse(request.Payload.GetRawText())));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_ForDistinctDispatches_UsesDistinctNonEmptyRequestIds ()
    {
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(request.RequestId));
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token"),
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var project = ResolvedUnityProjectContextTestFactory.Create();

        var firstResult = await client.SendAsync(
            project,
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);
        var secondResult = await client.SendAsync(
            project,
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Collection(
            transportClient.Requests,
            firstRequest => Assert.NotEqual(Guid.Empty, firstRequest.RequestId),
            secondRequest => Assert.NotEqual(Guid.Empty, secondRequest.RequestId));
        Assert.NotEqual(transportClient.Requests[0].RequestId, transportClient.Requests[1].RequestId);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenSessionTokenPublicationLags_ReResolvesWithoutReplayingRejectedToken (bool streaming)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        var rejectedDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        transportClient.EnqueueResponse(request =>
        {
            rejectedDispatch.TrySetResult();
            return CreateSessionTokenInvalidResponse();
        });
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionStore = new SignalingQueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming ? UnityIpcMethod.TestRun : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    CancellationToken.None)
                .AsTask();
        await rejectedDispatch.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var expectedReadCount = 2; expectedReadCount <= 4; expectedReadCount++)
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
            await sessionStore.WaitForReadCountAsync(expectedReadCount);
        }

        var result = await sendTask;

        Assert.True(result.IsSuccess);
        IpcRequestAssert.SessionTokens(
            transportClient.Requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(transportClient.Requests);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenSuccessorSessionTokenIsRejected_FollowsNextPublishedGeneration (bool streaming)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        var initialDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var successorDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        transportClient.EnqueueResponse(request =>
        {
            initialDispatch.TrySetResult();
            return CreateSessionTokenInvalidResponse();
        });
        transportClient.EnqueueResponse(_ =>
        {
            successorDispatch.TrySetResult();
            return CreateSessionTokenInvalidResponse();
        });
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionStore = new SuccessorPublicationDaemonSessionStore(
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-2"),
            CreateSessionReadResult("daemon-token-3"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming ? UnityIpcMethod.TestRun : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    CancellationToken.None)
                .AsTask();
        await initialDispatch.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        await sessionStore.FirstSuccessorPublicationRequested.WaitAsync(TimeSpan.FromSeconds(5));
        sessionStore.PublishFirstSuccessor();
        await successorDispatch.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        await sessionStore.SecondSuccessorPublicationRequested.WaitAsync(TimeSpan.FromSeconds(5));
        sessionStore.PublishSecondSuccessor();

        var result = await sendTask;

        Assert.True(result.IsSuccess);
        IpcRequestAssert.SessionTokens(
            transportClient.Requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-3").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(transportClient.Requests);
        Assert.True(
            timeProvider.GetUtcNow()
            < DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenRejectedSessionTokenDoesNotRotate_ReturnsRejectionAfterPublicationGrace (bool streaming)
    {
        var timeProvider = new PublicationGraceTimerObservingFakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(_ => CreateSessionTokenInvalidResponse());
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(CreateSessionReadResult("daemon-token"))));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming ? UnityIpcMethod.TestRun : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    CancellationToken.None)
                .AsTask();
        await timeProvider.PublicationRetryTimerRegistered.WaitAsync(SignalWaitTimeout);
        timeProvider.Advance(DaemonTimeouts.SessionPublicationRetryTimeout);

        var result = await sendTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(IpcSessionErrorCodes.SessionTokenInvalid, Assert.Single(result.Response!.Errors).Code);
        var request = Assert.Single(transportClient.Requests);
        Assert.Equal(
            IpcSessionTokenTestFactory.Create("daemon-token").GetEncodedValue(),
            request.SessionToken);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout,
            timeProvider.GetUtcNow());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenReplacementSessionResolutionIgnoresCancellation_ReturnsRejectionAtPublicationDeadline (
        bool streaming)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(_ => CreateSessionTokenInvalidResponse());
        var sessionStore = new NonCooperativeBlockingDaemonSessionStore(
            blockOnCall: 2,
            CreateSessionReadResult("daemon-token"),
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming ? UnityIpcMethod.TestRun : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);
        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    CancellationToken.None)
                .AsTask();
        await sessionStore.Blocked.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            timeProvider.Advance(DaemonTimeouts.SessionPublicationRetryTimeout);
            var completedTask = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(1)));

            Assert.Same(sendTask, completedTask);
            var result = await sendTask;
            Assert.True(result.IsSuccess);
            Assert.Equal(IpcSessionErrorCodes.SessionTokenInvalid, Assert.Single(result.Response!.Errors).Code);
            Assert.Single(transportClient.Requests);
            Assert.Equal(
                DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout,
                timeProvider.GetUtcNow());
        }
        finally
        {
            sessionStore.Release();
            await ObserveCompletionAsync(sendTask);
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    public async Task Send_WhenSuccessorEndpointFailsBeforeWrite_RetriesWithNextPublishedGeneration (
        bool streaming,
        bool recoverable,
        bool connectTimesOut)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var attempt = 0;
        ValueTask<IpcResponse> SendAttempt (
            IpcEndpoint _,
            IpcRequestEnvelope request,
            TimeSpan __,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Interlocked.Increment(ref attempt) switch
            {
                1 => ValueTask.FromResult(IpcResponseTestFactory.CreateError(
                    request,
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "Initial daemon session token is invalid.")),
                2 when connectTimesOut => throw new IpcConnectTimeoutException(
                    "IPC connection timed out before the request was sent."),
                2 => throw new IpcConnectException(
                    "IPC connection was refused before the request was sent.",
                    new SocketException((int)SocketError.ConnectionRefused)),
                _ => ValueTask.FromResult(CreateResponse(request.RequestId)),
            };
        }

        var transportClient = new StubIpcTransportClient
        {
            SendHandler = SendAttempt,
            StreamingHandler = (endpoint, request, timeout, _, cancellationToken) =>
                SendAttempt(endpoint, request, timeout, cancellationToken),
        };
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-2"),
            CreateSessionReadResult("daemon-token-3"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming
                ? UnityIpcMethod.TestRun
                : recoverable
                    ? UnityIpcMethod.PlayEnter
                    : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    CancellationToken.None)
                .AsTask();
        timeProvider.Advance(TimeSpan.FromSeconds(2));

        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = transportClient.Invocations.Select(static invocation => invocation.Request).ToArray();
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-3").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenLateSuccessorEndpointFailsBeforeWrite_OpensNewPublicationWindowForNextGeneration (
        bool streaming)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var attempt = 0;
        var initialDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var successorDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ValueTask<IpcResponse> SendAttempt (
            IpcEndpoint _,
            IpcRequestEnvelope request,
            TimeSpan __,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Interlocked.Increment(ref attempt) switch
            {
                1 => ReportInitialTokenRejection(request),
                2 => ThrowSuccessorConnectionRefusal(),
                _ => ValueTask.FromResult(CreateResponse(request.RequestId)),
            };
        }

        var transportClient = new StubIpcTransportClient
        {
            SendHandler = SendAttempt,
            StreamingHandler = (endpoint, request, timeout, _, cancellationToken) =>
                SendAttempt(endpoint, request, timeout, cancellationToken),
        };
        var sessionStore = new SuccessorPublicationDaemonSessionStore(
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-2"),
            CreateSessionReadResult("daemon-token-3"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming ? UnityIpcMethod.TestRun : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    CancellationToken.None)
                .AsTask();
        await initialDispatch.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        await sessionStore.FirstSuccessorPublicationRequested.WaitAsync(TimeSpan.FromSeconds(5));
        sessionStore.PublishFirstSuccessor();
        await successorDispatch.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        await sessionStore.SecondSuccessorPublicationRequested.WaitAsync(TimeSpan.FromSeconds(5));
        sessionStore.PublishSecondSuccessor();

        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = transportClient.Invocations.Select(static invocation => invocation.Request).ToArray();
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-3").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.True(
            timeProvider.GetUtcNow()
            < DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout);

        ValueTask<IpcResponse> ReportInitialTokenRejection (IpcRequestEnvelope request)
        {
            initialDispatch.TrySetResult();
            return ValueTask.FromResult(IpcResponseTestFactory.CreateError(
                request,
                IpcSessionErrorCodes.SessionTokenInvalid,
                "Initial daemon session token is invalid."));
        }

        ValueTask<IpcResponse> ThrowSuccessorConnectionRefusal ()
        {
            successorDispatch.TrySetResult();
            throw new IpcConnectException(
                "IPC connection was refused before the request was sent.",
                new SocketException((int)SocketError.ConnectionRefused));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Send_WhenNonRecoverableMutationResponseIsInterrupted_DoesNotReplayAmbiguousRequest ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(CreateSessionTokenInvalidResponse());
        transportClient.EnqueueResponse(static _ => throw new IpcResponseReadInterruptedException(
            new EndOfStreamException("lost console-clear response after session rotation")));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                CreateSessionReadResult("daemon-token-1"),
                CreateSessionReadResult("daemon-token-2"),
                CreateSessionReadResult("daemon-token-3"))));
        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                new UnityIpcDispatchRequest(
                    UnityIpcMethod.UnityConsoleClear,
                    CreateDispatchPayload(),
                    UnityBatchmodeLaunchOptions.Default),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        var result = await sendTask;

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, result.FailureInfo!.FailureKind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        var requests = transportClient.Requests;
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Send_WhenRecoverableResponseInterruptionOutlivesEndpointWindow_PreservesOriginalInterruption ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var interruption = new IpcResponseReadInterruptedException(
            new EndOfStreamException("The recoverable response stream was interrupted."));
        var transportClient = new RecordingIpcTransportClient(_ => throw interruption);
        var session = DaemonSessionTestFactory.CreateForToken("daemon-token");
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadHandler = invocations => invocations.Count == 1
                ? DaemonSessionReadResultTestFactory.Found(session)
                : DaemonSessionReadResult.Missing(),
        };
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(sessionStore));

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                CreateLifecycleDispatchRequest(
                    LifecycleExecutionKind.PlayEnter,
                    timeProvider,
                    TimeSpan.FromSeconds(5)),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap);

        var result = await sendTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, result.FailureInfo!.FailureKind);
        Assert.Equal(EditorLifecycleErrorCodes.EditorUnavailable, result.ErrorCode);
        Assert.Contains(interruption.Message, result.Message, StringComparison.Ordinal);
        var actionRequests = IpcRequestAssert.ActionRequests(transportClient.Requests);
        Assert.Single(actionRequests);
        _ = IpcRequestAssert.SingleRequestId(actionRequests);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + DaemonTimeouts.ProbeAttemptTimeoutCap,
            timeProvider.GetUtcNow());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Send_WhenSessionTokenRotates_RetriesThroughTheRequestedTransport (
        bool streaming,
        bool recoverable)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(CreateSessionTokenInvalidResponse());
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming
                ? UnityIpcMethod.TestRun
                : recoverable
                    ? UnityIpcMethod.PlayEnter
                    : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    CancellationToken.None)
                .AsTask();
        var result = await sendTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, transportClient.Requests.Count);
        Assert.Equal(
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            transportClient.Requests[0].SessionToken);
        Assert.Equal(
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            transportClient.Requests[1].SessionToken);
        _ = IpcRequestAssert.SingleRequestId(transportClient.Requests);
        Assert.Equal(streaming ? 2 : 0, transportClient.StreamingRequests.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WithRequestDeadline_PreservesAbsoluteDeadlineAlongsideTransportTimeout ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var response = CreateResponse(Guid.NewGuid());
        var transportClient = new RecordingIpcTransportClient(_ => response);
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = CreateDispatchRequest();

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            dispatchRequest,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(30), Assert.Single(transportClient.Timeouts));
        var request = Assert.Single(transportClient.Requests);
        Assert.Equal(DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(30), request.RequestDeadlineUtc);
        Assert.False(request.Payload.TryGetProperty("timeoutMilliseconds", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WithNewLifecycleExecution_UsesCompletionDeadlineForIpcDelivery ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(
            request => CreateResponse(request.RequestId));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    CreateSessionReadResult("daemon-token"))));
        var dispatchRequest = CreateLifecycleDispatchRequest(
            LifecycleExecutionKind.Compile,
            timeProvider,
            TimeSpan.FromSeconds(30));
        var executionDeadline =
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider);

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            dispatchRequest,
            executionDeadline,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Collection(
            transportClient.Requests,
            lifecycleStartRequest =>
            {
                Assert.Equal(
                    DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(33),
                    lifecycleStartRequest.RequestDeadlineUtc);
                Assert.True(IpcPayloadCodec.TryDeserialize(
                    lifecycleStartRequest.Payload,
                    out IpcLifecycleExecutionStartRequest start,
                    out var readError),
                    readError.Message);
                Assert.Equal(
                    DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(30),
                    start.DeadlineUtc);
            },
            actionRequest => Assert.Equal(
                DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(33),
                actionRequest.RequestDeadlineUtc));
        Assert.All(
            transportClient.Timeouts,
            timeout => Assert.Equal(TimeSpan.FromSeconds(33), timeout));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenCallerCancelsAfterLifecycleStart_KeepsActionDispatchRunning ()
    {
        using var callerCancellation = new CancellationTokenSource();
        var actionResponseSource =
            new TaskCompletionSource<IpcResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var transportClient = new RecordingIpcTransportClient(
            (request, _) =>
            {
                callerCancellation.Cancel();
                return new ValueTask<IpcResponse>(actionResponseSource.Task);
            });
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    CreateSessionReadResult("daemon-token"))));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateLifecycleDispatchRequest(LifecycleExecutionKind.Compile),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            callerCancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.ErrorCode);
        Assert.NotNull(result.LifecycleExecutionStart);
        Assert.True(result.LifecycleActionDispatched);
        var actionRequest = Assert.Single(
            IpcRequestAssert.ActionRequests(transportClient.Requests));
        Assert.Equal(TextVocabulary.GetText(UnityIpcMethod.Compile), actionRequest.Method);
        Assert.False(transportClient.CancellationTokens[^1].CanBeCanceled);

        actionResponseSource.TrySetResult(CreateResponse(actionRequest.RequestId));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenExecutionDeadlineExpiresAfterStartWrite_UsesCompletionGraceForActionDelivery ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(
            request =>
            {
                switch (IpcRequestAssert.ParseMethod(request))
                {
                    case UnityIpcMethod.LifecycleStart:
                        var response =
                            LifecycleExecutionIpcTestResponseFactory.TryCreateResponse(request)!;
                        timeProvider.Advance(TimeSpan.FromSeconds(31));
                        return response;
                    case UnityIpcMethod.Compile:
                        return CreateResponse(request.RequestId);
                    default:
                        throw new Xunit.Sdk.XunitException(
                            $"Unexpected method: {request.Method}");
                }
            },
            createLifecycleStartResponses: false);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    CreateSessionReadResult("daemon-token"))));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateLifecycleDispatchRequest(
                LifecycleExecutionKind.Compile,
                timeProvider,
                TimeSpan.FromSeconds(30)),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.LifecycleExecutionStart);
        Assert.True(result.LifecycleActionDispatched);
        var actionRequest = Assert.Single(
            IpcRequestAssert.ActionRequests(transportClient.Requests));
        Assert.Equal(
            DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(33),
            actionRequest.RequestDeadlineUtc);
        Assert.Equal(
            TimeSpan.FromSeconds(2),
            transportClient.Timeouts[^1]);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenSessionResolutionConsumesBudget_PassesCurrentRemainingTimeoutToTransport (bool streaming)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(request.RequestId));
        var sessionStore = new TimeAdvancingDaemonSessionStore(
            timeProvider,
            TimeSpan.FromSeconds(2),
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming ? UnityIpcMethod.TestRun : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var result = streaming
            ? await client.SendStreamingAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                dispatchRequest,
                deadline,
                (_, _) => ValueTask.CompletedTask,
                CancellationToken.None)
            : await client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                dispatchRequest,
                deadline,
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(3), Assert.Single(transportClient.Timeouts));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenSessionResolutionDoesNotCompleteBeforeDeadline_ReturnsIpcTimeout (bool streaming)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(request.RequestId));
        var sessionStore = new BlockingDaemonSessionStore(
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming ? UnityIpcMethod.TestRun : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);
        using var cleanupCancellationTokenSource = new CancellationTokenSource();
        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    cleanupCancellationTokenSource.Token)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    cleanupCancellationTokenSource.Token)
                .AsTask();
        await sessionStore.Started;
        try
        {
            timeProvider.Advance(TimeSpan.FromSeconds(5));
            var result = await sendTask.WaitAsync(SignalWaitTimeout);
            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
            await sessionStore.CancellationObserved.WaitAsync(SignalWaitTimeout);
            DaemonIpcDispatchAssert.NoDispatchWasSent(transportClient);
        }
        finally
        {
            cleanupCancellationTokenSource.Cancel();
            sessionStore.Release();
            try
            {
                await sendTask;
            }
            catch (OperationCanceledException) when (cleanupCancellationTokenSource.IsCancellationRequested)
            {
            }
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenSessionResolutionCancellationCallbackBlocks_ReturnsAtDeadline (bool streaming)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(request.RequestId));
        var sessionStore = new BlockingCancellationCallbackDaemonSessionStore(
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming ? UnityIpcMethod.TestRun : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var timeout = TimeSpan.FromSeconds(5);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    CancellationToken.None)
                .AsTask();

        try
        {
            await sessionStore.Started.WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(timeout);
            await sessionStore.CancellationCallbackStarted.WaitAsync(TimeSpan.FromSeconds(1));

            var completedTask = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(sendTask, completedTask);
            var result = await sendTask;
            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
            DaemonIpcDispatchAssert.NoDispatchWasSent(transportClient);
        }
        finally
        {
            sessionStore.ReleaseCancellationCallback();
            sessionStore.ReleaseResolution();
            await ObserveCompletionAsync(sendTask);
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenCallerCancelsDuringSessionResolution_PropagatesCancellation (bool streaming)
    {
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(request.RequestId));
        var sessionStore = new BlockingDaemonSessionStore(
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming ? UnityIpcMethod.TestRun : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System);
        using var cancellationTokenSource = new CancellationTokenSource();
        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    cancellationTokenSource.Token)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    cancellationTokenSource.Token)
                .AsTask();
        await sessionStore.Started;

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sendTask);
        await sessionStore.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(1));
        DaemonIpcDispatchAssert.NoDispatchWasSent(transportClient);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Send_WhenLatestRejectedSessionTokenDoesNotRotate_ReturnsLatestRejectionAfterItsPublicationGrace (
        bool streaming,
        bool recoverable)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var secondDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatchCount = 0;
        var transportClient = new RecordingIpcTransportClient(_ =>
        {
            if (Interlocked.Increment(ref dispatchCount) == 2)
            {
                secondDispatch.TrySetResult();
            }

            return CreateSessionTokenInvalidResponse();
        });
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming
                ? UnityIpcMethod.TestRun
                : recoverable
                    ? UnityIpcMethod.PlayEnter
                    : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var deadline = ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider);

        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    CancellationToken.None)
                .AsTask();
        await secondDispatch.Task.WaitAsync(TimeSpan.FromSeconds(5));
        timeProvider.Advance(DaemonTimeouts.SessionPublicationRetryTimeout);

        var result = await sendTask;
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.NotEmpty(result.Response!.Errors);
        Assert.Equal(IpcSessionErrorCodes.SessionTokenInvalid, Assert.Single(result.Response.Errors).Code);
        Assert.Equal(2, transportClient.Requests.Count);
        Assert.Equal(
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            transportClient.Requests[0].SessionToken);
        Assert.Equal(
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            transportClient.Requests[1].SessionToken);
        _ = IpcRequestAssert.SingleRequestId(transportClient.Requests);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout,
            timeProvider.GetUtcNow());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenRecoveryLifecycleReadIgnoresCancellation_ReturnsTokenRejectionAtPublicationWindow (bool streaming)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(_ => CreateSessionTokenInvalidResponse());
        var lifecycleReadStartedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycleReadReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadAsyncHandler = async (_, _, _) =>
            {
                lifecycleReadStartedSource.TrySetResult(true);
                await lifecycleReadReleaseSource.Task;
                return DaemonLifecycleObservationReadResult.Success(null);
            },
        };
        var recoveryWaiter = new DaemonSessionRecoveryWaiter(
            lifecycleStore,
            new RecordingDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess));
        var currentSession = DaemonSessionTestFactory.Create(
            sessionToken: "daemon-token",
            editorMode: UnityEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(currentSession)),
                recoveryWaiter));
        var dispatchRequest = new UnityIpcDispatchRequest(
            streaming ? UnityIpcMethod.TestRun : UnityIpcMethod.OpsRead,
            CreateDispatchPayload(),
            UnityBatchmodeLaunchOptions.Default);
        var timeout = TimeSpan.FromSeconds(5);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var sendTask = streaming
            ? client.SendStreamingAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    (_, _) => ValueTask.CompletedTask,
                    CancellationToken.None)
                .AsTask()
            : client.SendAsync(
                    ResolvedUnityProjectContextTestFactory.Create(),
                    dispatchRequest,
                    deadline,
                    CancellationToken.None)
                .AsTask();

        try
        {
            await lifecycleReadStartedSource.Task.WaitAsync(TimeSpan.FromSeconds(1));
            timeProvider.Advance(DaemonTimeouts.SessionPublicationRetryTimeout);

            var result = await sendTask;
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Response);
            Assert.Equal(
                IpcSessionErrorCodes.SessionTokenInvalid,
                Assert.Single(result.Response!.Errors).Code);
            Assert.Single(transportClient.Requests);
            Assert.True(
                timeProvider.GetUtcNow()
                >= DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout);
            Assert.True(timeProvider.GetUtcNow() < DateTimeOffset.UnixEpoch + timeout);
        }
        finally
        {
            lifecycleReadReleaseSource.TrySetResult(true);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionTokenIsNotAvailable_ReturnsFailureWithoutCallingTransport ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        var sessionStore = new QueuedDaemonSessionStore(
            DaemonSessionReadResult.Missing());
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        DaemonIpcDispatchAssert.NoDispatchWasSent(transportClient);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenTransportTimesOut_ReturnsIpcTimeout ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => throw new TimeoutException("timed out"));
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token"));
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            new UnityIpcDispatchRequest(
                UnityIpcMethod.UnityConsoleClear,
                CreateDispatchPayload(),
                UnityBatchmodeLaunchOptions.Default),
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, result.FailureInfo!.FailureKind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        _ = Assert.Single(transportClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenNonRecoverableDispatchConnectAttemptTimesOut_RetriesBecauseRequestWasNotSent ()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(request.RequestId));
        transportClient.EnqueueException(new IpcConnectTimeoutException("request was not sent"));
        transportClient.EnqueueResponse(request => CreateResponse(request.RequestId));
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                CreateDispatchRequest(),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RetriedDispatchesWithSameRequestId(
            transportClient,
            UnityIpcMethod.OpsRead,
            maximumAttempts: 2);
        Assert.Equal(2, requests.Count);
    }

    private sealed class PublicationGraceTimerObservingFakeTimeProvider : FakeTimeProvider
    {
        private readonly TaskCompletionSource publicationRetryTimerRegistered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PublicationGraceTimerObservingFakeTimeProvider (DateTimeOffset startTime)
            : base(startTime)
        {
        }

        public Task PublicationRetryTimerRegistered => publicationRetryTimerRegistered.Task;

        public override ITimer CreateTimer (
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            if (dueTime == TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds)
                && period == Timeout.InfiniteTimeSpan)
            {
                publicationRetryTimerRegistered.TrySetResult();
            }

            return timer;
        }
    }

    private sealed class TimeAdvancingDaemonSessionStore : ReadOnlyDaemonSessionStore
    {
        private readonly FakeTimeProvider timeProvider;

        private readonly TimeSpan elapsed;

        private readonly DaemonSessionReadResult result;

        public TimeAdvancingDaemonSessionStore (
            FakeTimeProvider timeProvider,
            TimeSpan elapsed,
            DaemonSessionReadResult result)
        {
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.elapsed = elapsed;
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public override ValueTask<DaemonSessionReadResult> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(elapsed);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class BlockingDaemonSessionStore : ReadOnlyDaemonSessionStore
    {
        private readonly DaemonSessionReadResult result;

        private readonly TaskCompletionSource<bool> startedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationObservedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> releaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingDaemonSessionStore (DaemonSessionReadResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public Task Started => startedSource.Task;

        public Task CancellationObserved => cancellationObservedSource.Task;

        public override async ValueTask<DaemonSessionReadResult> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            startedSource.TrySetResult(true);
            var cancellationSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                cancellationObservedSource.TrySetResult(true);
                cancellationSource.TrySetResult(true);
            });
            await Task.WhenAny(releaseSource.Task, cancellationSource.Task);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }

        public void Release ()
        {
            releaseSource.TrySetResult(true);
        }
    }

    private static async Task ObserveCompletionAsync (Task task)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(1)));
        if (ReferenceEquals(completedTask, task))
        {
            _ = task.Exception;
        }
    }

    private sealed class SignalingQueuedDaemonSessionStore : ReadOnlyDaemonSessionStore
    {
        private readonly Queue<DaemonSessionReadResult> results;

        private TaskCompletionSource<int> readCountSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private DaemonSessionReadResult lastResult;

        private int readCount;

        public SignalingQueuedDaemonSessionStore (params DaemonSessionReadResult[] results)
        {
            this.results = new Queue<DaemonSessionReadResult>(results);
            lastResult = results.Length == 0 ? DaemonSessionReadResult.Missing() : results[^1];
        }

        public int ReadCount => Volatile.Read(ref readCount);

        public async Task WaitForReadCountAsync (int expectedReadCount)
        {
            while (true)
            {
                if (ReadCount >= expectedReadCount)
                {
                    return;
                }

                var nextReadSource = Volatile.Read(ref readCountSource);
                if (ReadCount >= expectedReadCount)
                {
                    return;
                }

                try
                {
                    _ = await nextReadSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException exception)
                {
                    throw new TimeoutException(
                        $"Daemon session read {expectedReadCount} was not observed; observed {ReadCount} reads.",
                        exception);
                }
            }
        }

        public override ValueTask<DaemonSessionReadResult> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (results.Count != 0)
            {
                lastResult = results.Dequeue();
            }

            var nextReadSource = Interlocked.Exchange(
                ref readCountSource,
                new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously));
            _ = Interlocked.Increment(ref readCount);
            nextReadSource.TrySetResult(ReadCount);
            return ValueTask.FromResult(lastResult);
        }
    }

    private sealed class SuccessorPublicationDaemonSessionStore : ReadOnlyDaemonSessionStore
    {
        private readonly DaemonSessionReadResult initialResult;

        private readonly DaemonSessionReadResult firstSuccessorResult;

        private readonly DaemonSessionReadResult secondSuccessorResult;

        private readonly TaskCompletionSource firstSuccessorPublicationRequested =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource secondSuccessorPublicationRequested =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource firstSuccessorPublished =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource secondSuccessorPublished =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int readCount;

        public SuccessorPublicationDaemonSessionStore (
            DaemonSessionReadResult initialResult,
            DaemonSessionReadResult firstSuccessorResult,
            DaemonSessionReadResult secondSuccessorResult)
        {
            this.initialResult = initialResult;
            this.firstSuccessorResult = firstSuccessorResult;
            this.secondSuccessorResult = secondSuccessorResult;
        }

        public Task FirstSuccessorPublicationRequested => firstSuccessorPublicationRequested.Task;

        public Task SecondSuccessorPublicationRequested => secondSuccessorPublicationRequested.Task;

        public void PublishFirstSuccessor ()
        {
            firstSuccessorPublished.TrySetResult();
        }

        public void PublishSecondSuccessor ()
        {
            secondSuccessorPublished.TrySetResult();
        }

        public override async ValueTask<DaemonSessionReadResult> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Interlocked.Increment(ref readCount) switch
            {
                1 => initialResult,
                2 => await WaitForFirstSuccessorPublicationAsync(cancellationToken),
                _ => await WaitForSecondSuccessorPublicationAsync(cancellationToken),
            };
        }

        private async ValueTask<DaemonSessionReadResult> WaitForFirstSuccessorPublicationAsync (
            CancellationToken cancellationToken)
        {
            firstSuccessorPublicationRequested.TrySetResult();
            await firstSuccessorPublished.Task.WaitAsync(cancellationToken);
            return firstSuccessorResult;
        }

        private async ValueTask<DaemonSessionReadResult> WaitForSecondSuccessorPublicationAsync (
            CancellationToken cancellationToken)
        {
            secondSuccessorPublicationRequested.TrySetResult();
            await secondSuccessorPublished.Task.WaitAsync(cancellationToken);
            return secondSuccessorResult;
        }
    }

    private sealed class BlockingCancellationCallbackDaemonSessionStore : ReadOnlyDaemonSessionStore
    {
        private readonly DaemonSessionReadResult result;

        private readonly TaskCompletionSource<bool> startedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationCallbackStartedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationCallbackReleaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> resolutionReleaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingCancellationCallbackDaemonSessionStore (
            DaemonSessionReadResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public Task Started => startedSource.Task;

        public Task CancellationCallbackStarted => cancellationCallbackStartedSource.Task;

        public override async ValueTask<DaemonSessionReadResult> ReadAsync (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            startedSource.TrySetResult(true);
            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                cancellationCallbackStartedSource.TrySetResult(true);
                cancellationCallbackReleaseSource.Task.GetAwaiter().GetResult();
            });
            await resolutionReleaseSource.Task;
            return result;
        }

        public void ReleaseCancellationCallback ()
        {
            cancellationCallbackReleaseSource.TrySetResult(true);
        }

        public void ReleaseResolution ()
        {
            resolutionReleaseSource.TrySetResult(true);
        }
    }
}
