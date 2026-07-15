using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSuccessful_ResolvesSessionConnectionAndDelegatesToTransport ()
    {
        var response = CreateResponse(Guid.NewGuid());
        var transportClient = new RecordingIpcTransportClient(_ => response);
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: TimeProvider.System);

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertUnityResponse(response, result.Response);
        var request = DaemonIpcDispatchAssert.SingleDispatchSentToEndpoint(
            transportClient,
            "/tmp/ucli-session.sock",
            UnityIpcMethod.OpsRead,
            IpcSessionTokenTestFactory.Create("daemon-token").GetEncodedValue());
        Assert.Equal(CreateDispatchPayload().GetRawText(), request.Payload.GetRawText());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_ForDistinctDispatches_UsesDistinctNonEmptyRequestIds ()
    {
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(request.RequestId));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"),
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: TimeProvider.System);
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
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(CreateSessionTokenInvalidResponse());
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var client = new UnityDaemonIpcClient(
            transportClient,
            new QueuedDaemonSessionConnectionProvider(
                CreateConnectionResult("daemon-token-1"),
                CreateConnectionResult("daemon-token-1"),
                CreateConnectionResult("daemon-token-1"),
                CreateConnectionResult("daemon-token-2")),
            recoveryWaiter: null,
            timeProvider);
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
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            sendTask,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(100));

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
    public async Task Send_WhenRejectedSessionTokenDoesNotRotate_ReturnsRejectionAfterPublicationGrace (bool streaming)
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateSessionTokenInvalidResponse());
        var client = new UnityDaemonIpcClient(
            transportClient,
            new StaticDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token")),
            recoveryWaiter: null,
            timeProvider);
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
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            sendTask,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(100));

        var result = await sendTask;

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
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    public async Task Send_WhenDistinctTokenReplayTransportFails_DoesNotResolveOrSendThirdGeneration (
        bool streaming,
        bool recoverable,
        bool connectTimesOut)
    {
        var timeProvider = new ManualTimeProvider();
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
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"),
            CreateConnectionResult("daemon-token-3"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider);
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
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            sendTask,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(100));

        var result = await sendTask;

        Assert.False(result.IsSuccess);
        var requests = transportClient.Invocations.Select(static invocation => invocation.Request).ToArray();
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
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
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(CreateSessionTokenInvalidResponse());
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider);
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
        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);

        var result = await sendTask;

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
        var timeProvider = new ManualTimeProvider();
        var response = CreateResponse(Guid.NewGuid());
        var transportClient = new RecordingIpcTransportClient(_ => response);
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: timeProvider);
        var dispatchRequest = new UnityIpcRequestBuilder().Build(new UnityRequestPayload.Compile(RunIdTestValues.Compile));

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

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenSessionResolutionConsumesBudget_PassesCurrentRemainingTimeoutToTransport (bool streaming)
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(request.RequestId));
        var sessionConnectionProvider = new TimeAdvancingDaemonSessionConnectionProvider(
            timeProvider,
            TimeSpan.FromSeconds(2),
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: timeProvider);
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
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(request.RequestId));
        var sessionConnectionProvider = new BlockingDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: timeProvider);
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
        await sessionConnectionProvider.Started;
        await timeProvider
            .WaitForTimerDueWithinAsync(TimeSpan.FromSeconds(5))
            .WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            timeProvider.Advance(TimeSpan.FromSeconds(5));
            var result = await sendTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
            await sessionConnectionProvider.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(1));
            DaemonIpcDispatchAssert.NoDispatchWasSent(transportClient);
        }
        finally
        {
            cleanupCancellationTokenSource.Cancel();
            sessionConnectionProvider.Release();
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
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(request.RequestId));
        var sessionConnectionProvider = new BlockingCancellationCallbackDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: timeProvider);
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
            await sessionConnectionProvider.Started.WaitAsync(TimeSpan.FromSeconds(1));
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));

            timeProvider.Advance(timeout);
            await sessionConnectionProvider.CancellationCallbackStarted.WaitAsync(TimeSpan.FromSeconds(1));

            var completedTask = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(sendTask, completedTask);
            var result = await sendTask;
            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
            DaemonIpcDispatchAssert.NoDispatchWasSent(transportClient);
        }
        finally
        {
            sessionConnectionProvider.ReleaseCancellationCallback();
            sessionConnectionProvider.ReleaseResolution();
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
        var sessionConnectionProvider = new BlockingDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: TimeProvider.System);
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
        await sessionConnectionProvider.Started;

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sendTask);
        await sessionConnectionProvider.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(1));
        DaemonIpcDispatchAssert.NoDispatchWasSent(transportClient);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Send_WhenRotatedSessionTokenIsRejectedTwice_ReturnsSecondRejectionWithoutFurtherRetry (
        bool streaming,
        bool recoverable)
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateSessionTokenInvalidResponse());
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: timeProvider);
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
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            sendTask,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(100));

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
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_WhenSessionTokenRefreshConsumesDeadline_ReturnsTimeoutWithoutAdditionalRetryDelay (bool streaming)
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateSessionTokenInvalidResponse());
        var readStartedSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readReleaseSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionStore = new RecordingDaemonSessionStore
        {
            ReadAsyncHandler = async (_, _, _) =>
            {
                readStartedSource.TrySetResult(true);
                await readReleaseSource.Task;
                return DaemonSessionReadResult.Missing();
            },
        };
        var recoveryWaiter = new UnityDaemonRecoveryWaiter(
            sessionStore,
            new RecordingDaemonLifecycleStore
            {
                ReadResult = DaemonLifecycleObservationReadResult.Success(null),
            },
            new RecordingDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess));
        var client = new UnityDaemonIpcClient(
            transportClient,
            new StaticDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token")),
            recoveryWaiter,
            timeProvider);
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
            await readStartedSource.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));

            timeProvider.Advance(timeout);

            var completedTask = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(sendTask, completedTask);
            var result = await sendTask;
            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
            Assert.Single(transportClient.Requests);
        }
        finally
        {
            readReleaseSource.TrySetResult(true);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionTokenIsNotAvailable_ReturnsFailureWithoutCallingTransport ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            DaemonSessionConnectionResolutionResult.SessionNotAvailable());
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider: TimeProvider.System);

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
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var timeProvider = new ManualTimeProvider();
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider);

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateDispatchRequest(),
            ExecutionDeadline.Start(TimeSpan.FromMilliseconds(500), timeProvider),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        _ = Assert.Single(transportClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenNonRecoverableDispatchConnectAttemptTimesOut_RetriesBecauseRequestWasNotSent ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(request => CreateResponse(request.RequestId));
        transportClient.EnqueueException(new IpcConnectTimeoutException("request was not sent"));
        transportClient.EnqueueResponse(request => CreateResponse(request.RequestId));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter: null,
            timeProvider);

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                CreateDispatchRequest(),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        await timeProvider
            .WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds))
            .WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RetriedDispatchesWithSameRequestId(
            transportClient,
            UnityIpcMethod.OpsRead,
            maximumAttempts: 2);
        Assert.Equal(2, requests.Count);
    }

    private sealed class TimeAdvancingDaemonSessionConnectionProvider : IDaemonSessionConnectionProvider
    {
        private readonly ManualTimeProvider timeProvider;

        private readonly TimeSpan elapsed;

        private readonly DaemonSessionConnectionResolutionResult result;

        public TimeAdvancingDaemonSessionConnectionProvider (
            ManualTimeProvider timeProvider,
            TimeSpan elapsed,
            DaemonSessionConnectionResolutionResult result)
        {
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.elapsed = elapsed;
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public ValueTask<DaemonSessionConnectionResolutionResult> ResolveAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
            cancellationToken.ThrowIfCancellationRequested();
            timeProvider.Advance(elapsed);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class BlockingDaemonSessionConnectionProvider : IDaemonSessionConnectionProvider
    {
        private readonly DaemonSessionConnectionResolutionResult result;

        private readonly TaskCompletionSource<bool> startedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationObservedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> releaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingDaemonSessionConnectionProvider (DaemonSessionConnectionResolutionResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public Task Started => startedSource.Task;

        public Task CancellationObserved => cancellationObservedSource.Task;

        public async ValueTask<DaemonSessionConnectionResolutionResult> ResolveAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
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

    private sealed class BlockingCancellationCallbackDaemonSessionConnectionProvider : IDaemonSessionConnectionProvider
    {
        private readonly DaemonSessionConnectionResolutionResult result;

        private readonly TaskCompletionSource<bool> startedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationCallbackStartedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationCallbackReleaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> resolutionReleaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingCancellationCallbackDaemonSessionConnectionProvider (
            DaemonSessionConnectionResolutionResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public Task Started => startedSource.Task;

        public Task CancellationCallbackStarted => cancellationCallbackStartedSource.Task;

        public async ValueTask<DaemonSessionConnectionResolutionResult> ResolveAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
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
