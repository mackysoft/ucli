using System.Net.Sockets;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Features.Daemon.Common.Ipc;

public sealed class DaemonIpcRequestSenderTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonElement RequestPayload = JsonDocument.Parse("{}").RootElement.Clone();

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionIsMissing_ReturnsDaemonSessionNotAvailableWithoutTransportCall ()
    {
        var transportClient = new UnexpectedIpcTransportClient("Missing daemon session must stop before sending IPC requests.");
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResult.Missing())),
            new DaemonReachabilityClassifier(),
            TimeProvider.System);

        var result = await sender.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UnityIpcMethod.UnityLogsRead,
            RequestPayload,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonSessionNotAvailable, error.Code);
        Assert.Contains("--projectPath", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityIpcMethod.DaemonLogsRead)]
    [InlineData(UnityIpcMethod.UnityLogsRead)]
    public async Task SendAsync_WhenStatelessReadAttemptTimesOutEarly_ReplaysWithinOriginalDeadline (
        UnityIpcMethod method)
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueException(new TimeoutException("attempt timed out"));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(CreateSessionReadResult("daemon-token"))),
            new DaemonReachabilityClassifier(),
            timeProvider);

        var resultTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                method,
                RequestPayload,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await resultTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(transportClient, method, method);
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.All(
            requests,
            request => Assert.Equal(DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(5), request.RequestDeadlineUtc));
        Assert.True(transportClient.Timeouts[1] < transportClient.Timeouts[0]);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityIpcMethod.DaemonLogsRead)]
    [InlineData(UnityIpcMethod.UnityLogsRead)]
    public async Task SendAsync_WhenStatelessReadResponseIsInterrupted_ReplaysWithSameRequestIdentity (
        UnityIpcMethod method)
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueException(new IpcResponseReadInterruptedException(
            new IOException("response read was interrupted")));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(CreateSessionReadResult("daemon-token"))),
            new DaemonReachabilityClassifier(),
            timeProvider);

        var resultTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                method,
                RequestPayload,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await resultTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(transportClient, method, method);
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.All(
            requests,
            request => Assert.Equal(DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(5), request.RequestDeadlineUtc));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenMutationResponseIsInterrupted_DoesNotReplay ()
    {
        var transportClient = CreateTransportClient();
        transportClient.EnqueueException(new IpcResponseReadInterruptedException(
            new IOException("console clear response was interrupted")));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(CreateSessionReadResult("daemon-token"))),
            new DaemonReachabilityClassifier(),
            TimeProvider.System);

        var result = await sender.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UnityIpcMethod.UnityConsoleClear,
            RequestPayload,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Single(transportClient.Requests);
        Assert.Contains(
            "console clear response was interrupted",
            Assert.IsType<ExecutionError>(result.Error).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenStatelessReadRecoveryWindowExpires_PreservesOriginalInterruption ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueException(new IpcResponseReadInterruptedException(
            new IOException("original response interruption")));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    CreateSessionReadResult("daemon-token"),
                    DaemonSessionReadResult.Missing())),
            new DaemonReachabilityClassifier(),
            timeProvider);

        var resultTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await TestAwaiter.WaitAsync(
            timeProvider.WaitForTimerDueWithinAsync(retryDelay),
            "stateless read recovery retry timer",
            AsyncWaitTimeout);
        timeProvider.Advance(DaemonTimeouts.SessionPublicationRetryTimeout);
        var result = await TestAwaiter.WaitAsync(
            resultTask,
            "stateless read recovery window result",
            AsyncWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.Single(transportClient.Requests);
        Assert.Contains(
            "original response interruption",
            Assert.IsType<ExecutionError>(result.Error).Message,
            StringComparison.Ordinal);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout,
            timeProvider.GetUtcNow());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenEarlyTimeoutRecoveryWindowExpires_PreservesAttemptTimeoutMessage ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueException(new TimeoutException("original attempt timeout"));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    CreateSessionReadResult("daemon-token"),
                    DaemonSessionReadResult.Missing())),
            new DaemonReachabilityClassifier(),
            timeProvider);

        var resultTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.DaemonLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await TestAwaiter.WaitAsync(
            timeProvider.WaitForTimerDueWithinAsync(retryDelay),
            "early timeout recovery retry timer",
            AsyncWaitTimeout);
        timeProvider.Advance(DaemonTimeouts.SessionPublicationRetryTimeout);
        var result = await TestAwaiter.WaitAsync(
            resultTask,
            "early timeout recovery window result",
            AsyncWaitTimeout);

        Assert.False(result.IsSuccess);
        Assert.Single(transportClient.Requests);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("original attempt timeout", error.Message, StringComparison.Ordinal);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout,
            timeProvider.GetUtcNow());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendAsync_WhenSessionResolutionDoesNotQuiesce_ReturnsTimeoutAtDeadline (
        bool blockCancellationCallback)
    {
        var timeProvider = new ManualTimeProvider();
        var sessionStore = new BlockingDaemonSessionStore(
            CreateSessionReadResult("daemon-token"),
            blockCancellationCallback);
        var sender = new DaemonIpcRequestSender(
            new UnexpectedIpcTransportClient("Timed-out session resolution must stop before transport dispatch."),
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore),
            new DaemonReachabilityClassifier(),
            timeProvider);
        var timeout = TimeSpan.FromSeconds(5);
        var sendTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                timeout,
                CancellationToken.None)
            .AsTask();

        try
        {
            await sessionStore.Started.WaitAsync(TimeSpan.FromSeconds(1));
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));

            timeProvider.Advance(timeout);
            if (blockCancellationCallback)
            {
                await sessionStore.CancellationCallbackStarted.WaitAsync(TimeSpan.FromSeconds(1));
            }

            var completedTask = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(sendTask, completedTask);
            var result = await sendTask;
            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, Assert.IsType<ExecutionError>(result.Error).Kind);
        }
        finally
        {
            sessionStore.ReleaseCancellationCallback();
            sessionStore.ReleaseResolution();
            await ObserveCompletionAsync(sendTask);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionResolutionConsumesBudget_PassesRemainingTimeoutToTransport ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new TimeAdvancingDaemonSessionStore(
                timeProvider,
                TimeSpan.FromSeconds(2),
                CreateSessionReadResult("daemon-token"))),
            new DaemonReachabilityClassifier(),
            timeProvider);

        var result = await sender.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UnityIpcMethod.UnityLogsRead,
            RequestPayload,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(3), Assert.Single(transportClient.Timeouts));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoveryLifecycleReadIgnoresCancellation_ReturnsSessionNotAvailableAtEndpointWindow ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueException(new IpcConnectException(
            "IPC connection was refused before the request was sent.",
            new SocketException((int)SocketError.ConnectionRefused)));
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
        var currentSession = CreateGuiSession("daemon-token", Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(DaemonSessionReadResultTestFactory.Found(currentSession)),
            recoveryWaiter),
            new DaemonReachabilityClassifier(),
            timeProvider);
        var timeout = TimeSpan.FromSeconds(5);
        var sendTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                timeout,
                CancellationToken.None)
            .AsTask();

        try
        {
            await lifecycleReadStartedSource.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await timeProvider
                .WaitForTimerDueWithinAsync(DaemonTimeouts.ProbeAttemptTimeoutCap)
                .WaitAsync(TimeSpan.FromSeconds(1));

            timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap);

            var completedTask = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(sendTask, completedTask);
            var result = await sendTask;
            Assert.False(result.IsSuccess);
            var error = Assert.IsType<ExecutionError>(result.Error);
            Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
            Assert.Equal(DaemonErrorCodes.DaemonSessionNotAvailable, error.Code);
            Assert.Single(transportClient.Requests);
            Assert.Equal(
                DateTimeOffset.UnixEpoch + DaemonTimeouts.ProbeAttemptTimeoutCap,
                timeProvider.GetUtcNow());
            Assert.True(timeProvider.GetUtcNow() < DateTimeOffset.UnixEpoch + timeout);
        }
        finally
        {
            lifecycleReadReleaseSource.TrySetResult(true);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenConnectionIsRefusedDuringRecovery_RetriesWithReloadedSessionToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueException(new IpcConnectException(
            "IPC connection was refused before the request was sent.",
            new SocketException((int)SocketError.ConnectionRefused)));
        transportClient.EnqueueResponse(static request => CreateResponse(request.RequestId));
        var session = CreateGuiSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var successorSession = CreateGuiSession(
            "daemon-token-2",
            Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var lifecycleStore = new RecordingDaemonLifecycleStore
        {
            ReadResult = DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session)),
        };
        var recoveryWaiter = new DaemonSessionRecoveryWaiter(
            lifecycleStore,
            new RecordingDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess));
        var sessionStore = new QueuedDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(session),
            DaemonSessionReadResultTestFactory.Found(successorSession));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore,
            recoveryWaiter),
            new DaemonReachabilityClassifier(),
            timeProvider);

        var sendTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
            CancellationToken.None)
            .AsTask();
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(transportClient, UnityIpcMethod.UnityLogsRead, UnityIpcMethod.UnityLogsRead);
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        Assert.All(
            requests,
            request => Assert.Equal(
                DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(5),
                request.RequestDeadlineUtc));
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.Equal(RequestPayload.GetRawText(), requests[0].Payload.GetRawText());
        Assert.Equal(requests[0].Payload.GetRawText(), requests[1].Payload.GetRawText());
        Assert.Equal(
            requests[0].RequestDeadlineRemainingMilliseconds,
            requests[1].RequestDeadlineRemainingMilliseconds);
        Assert.Empty(lifecycleStore.ReadInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionTokenIsRejected_RetriesOnceWithReloadedSuccessorToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueResponse(static request => CreateSessionTokenInvalidResponse(request.RequestId));
        transportClient.EnqueueResponse(static request => CreateResponse(request.RequestId));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                CreateSessionReadResult("daemon-token-1"),
                CreateSessionReadResult("daemon-token-2"))),
            new DaemonReachabilityClassifier(),
            timeProvider);
        var resultTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
            CancellationToken.None)
            .AsTask();
        var result = await resultTask.WaitAsync(AsyncWaitTimeout);

        Assert.True(result.IsSuccess);
        Assert.Equal(IpcResponseStatus.Ok, result.Response!.Status);
        var requests = IpcRequestAssert.Methods(transportClient, UnityIpcMethod.UnityLogsRead, UnityIpcMethod.UnityLogsRead);
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        Assert.All(
            requests,
            request => Assert.Equal(
                DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(5),
                request.RequestDeadlineUtc));
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.Equal(requests[0].Method, requests[1].Method);
        Assert.Equal(requests[0].Payload.GetRawText(), requests[1].Payload.GetRawText());
        Assert.Equal(
            requests[0].RequestDeadlineRemainingMilliseconds,
            requests[1].RequestDeadlineRemainingMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionTokenPublicationLags_ReResolvesWithoutReplayingRejectedToken ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueResponse(static request => CreateSessionTokenInvalidResponse(request.RequestId));
        transportClient.EnqueueResponse(static request => CreateResponse(request.RequestId));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                CreateSessionReadResult("daemon-token-1"),
                CreateSessionReadResult("daemon-token-1"),
                CreateSessionReadResult("daemon-token-1"),
                CreateSessionReadResult("daemon-token-2"),
                CreateSessionReadResult("daemon-token-3"))),
            new DaemonReachabilityClassifier(),
            timeProvider);

        var resultTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await resultTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(IpcResponseStatus.Ok, result.Response!.Status);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.UnityLogsRead,
            UnityIpcMethod.UnityLogsRead);
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSuccessorSessionTokenIsRejected_FollowsNextPublishedGeneration ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueResponse(static request => CreateSessionTokenInvalidResponse(request.RequestId));
        transportClient.EnqueueResponse(static request => CreateSessionTokenInvalidResponse(request.RequestId));
        transportClient.EnqueueResponse(static request => CreateResponse(request.RequestId));
        var sessionConnections = Enumerable
            .Repeat(CreateSessionReadResult("daemon-token-1"), 18)
            .Append(CreateSessionReadResult("daemon-token-2"))
            .Append(CreateSessionReadResult("daemon-token-2"))
            .Append(CreateSessionReadResult("daemon-token-3"))
            .ToArray();
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(sessionConnections)),
            new DaemonReachabilityClassifier(),
            timeProvider);

        var resultTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await resultTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(IpcResponseStatus.Ok, result.Response!.Status);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.UnityLogsRead,
            UnityIpcMethod.UnityLogsRead,
            UnityIpcMethod.UnityLogsRead);
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-3").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.True(
            timeProvider.GetUtcNow()
            < DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSuccessorEndpointFailsBeforeWrite_RetriesWithNextPublishedGeneration ()
    {
        var timeProvider = new ManualTimeProvider();
        var attempt = 0;
        var transportClient = new RecordingIpcTransportClient(request =>
        {
            return Interlocked.Increment(ref attempt) switch
            {
                1 => CreateSessionTokenInvalidResponse(request.RequestId),
                2 => throw new IpcConnectException(
                    "IPC connection was refused before the request was sent.",
                    new SocketException((int)SocketError.ConnectionRefused)),
                _ => CreateResponse(request.RequestId),
            };
        });
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                CreateSessionReadResult("daemon-token-1"),
                CreateSessionReadResult("daemon-token-2"),
                CreateSessionReadResult("daemon-token-3"))),
            new DaemonReachabilityClassifier(),
            timeProvider);

        var resultTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await resultTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.UnityLogsRead,
            UnityIpcMethod.UnityLogsRead,
            UnityIpcMethod.UnityLogsRead);
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-3").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenLateSuccessorEndpointFailsBeforeWrite_OpensNewPublicationWindowForNextGeneration ()
    {
        var timeProvider = new ManualTimeProvider();
        var attempt = 0;
        var transportClient = new RecordingIpcTransportClient(request =>
        {
            return Interlocked.Increment(ref attempt) switch
            {
                1 => CreateSessionTokenInvalidResponse(request.RequestId),
                2 => throw new IpcConnectException(
                    "IPC connection was refused before the request was sent.",
                    new SocketException((int)SocketError.ConnectionRefused)),
                _ => CreateResponse(request.RequestId),
            };
        });
        var sessionConnections = Enumerable
            .Repeat(CreateSessionReadResult("daemon-token-1"), 19)
            .Append(CreateSessionReadResult("daemon-token-2"))
            .Append(CreateSessionReadResult("daemon-token-3"))
            .ToArray();
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(sessionConnections)),
            new DaemonReachabilityClassifier(),
            timeProvider);

        var resultTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            resultTask,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

        var result = await resultTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(IpcResponseStatus.Ok, result.Response!.Status);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.UnityLogsRead,
            UnityIpcMethod.UnityLogsRead,
            UnityIpcMethod.UnityLogsRead);
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-3").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.True(
            timeProvider.GetUtcNow()
            < DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRejectedSessionTokenDoesNotRotate_ReturnsRejectionAfterPublicationGrace ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueResponse(static request => CreateSessionTokenInvalidResponse(request.RequestId));
        transportClient.EnqueueResponse(static request => CreateResponse(request.RequestId));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(CreateSessionReadResult("daemon-token"))),
            new DaemonReachabilityClassifier(),
            timeProvider);

        var resultTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        await timeProvider
            .WaitForTimerDueWithinAsync(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds))
            .WaitAsync(AsyncWaitTimeout);
        timeProvider.Advance(DaemonTimeouts.SessionPublicationRetryTimeout);

        var result = await resultTask.WaitAsync(AsyncWaitTimeout);

        Assert.True(result.IsSuccess);
        Assert.Equal(IpcResponseStatus.Error, result.Response!.Status);
        Assert.Equal(IpcSessionErrorCodes.SessionTokenInvalid, Assert.Single(result.Response.Errors).Code);
        var request = Assert.Single(transportClient.Requests);
        Assert.Equal(
            IpcSessionTokenTestFactory.Create("daemon-token").GetEncodedValue(),
            request.SessionToken);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + DaemonTimeouts.SessionPublicationRetryTimeout,
            timeProvider.GetUtcNow());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenReplacementSessionResolutionIgnoresCancellation_ReturnsRejectionAtPublicationDeadline ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueResponse(static request => CreateSessionTokenInvalidResponse(request.RequestId));
        var sessionStore = new NonCooperativeBlockingDaemonSessionStore(
            blockOnCall: 2,
            CreateSessionReadResult("daemon-token"),
            CreateSessionReadResult("daemon-token"));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore),
            new DaemonReachabilityClassifier(),
            timeProvider);
        var sendTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
            CancellationToken.None)
            .AsTask();
        await sessionStore.Blocked.WaitAsync(AsyncWaitTimeout);

        try
        {
            await timeProvider
                .WaitForTimerDueWithinAsync(DaemonTimeouts.SessionPublicationRetryTimeout)
                .WaitAsync(AsyncWaitTimeout);
            timeProvider.Advance(DaemonTimeouts.SessionPublicationRetryTimeout);
            var completedTask = await Task.WhenAny(sendTask, Task.Delay(AsyncWaitTimeout));

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

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenEndpointRemainsAbsent_ReturnsDaemonSessionNotAvailable ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        for (var i = 0; i < 20; i++)
        {
            transportClient.EnqueueException(i % 2 == 0
                ? new IpcConnectTimeoutException("connection attempt timed out before request write")
                : new IpcConnectException(
                    "IPC connection was refused before the request was sent.",
                    new SocketException((int)SocketError.ConnectionRefused)));
        }

        var sender = new DaemonIpcRequestSender(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new RecordingDaemonSessionStore(CreateSessionReadResult("daemon-token"))),
            new DaemonReachabilityClassifier(),
            timeProvider: timeProvider);

        var sendTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await TestAwaiter.WaitAsync(
            timeProvider.WaitForTimerDueWithinAsync(retryDelay),
            "daemon IPC sender first endpoint retry timer",
            AsyncWaitTimeout);
        timeProvider.Advance(retryDelay);
        await TestAwaiter.WaitAsync(
            timeProvider.WaitForTimerDueWithinAsync(retryDelay),
            "daemon IPC sender second endpoint retry timer",
            AsyncWaitTimeout);
        timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap - retryDelay);

        var result = await TestAwaiter.WaitAsync(
            sendTask,
            "daemon IPC sender endpoint window result",
            AsyncWaitTimeout);

        Assert.False(result.IsSuccess);
        var requests = IpcRequestAssert.RetriedAtLeastOnce(transportClient);
        Assert.All(
            requests,
            request => Assert.Equal(ContractLiteralCodec.ToValue(UnityIpcMethod.UnityLogsRead), request.Method));
        var requestId = IpcRequestAssert.SingleRequestId(requests);
        Assert.NotEqual(Guid.Empty, requestId);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonSessionNotAvailable, error.Code);
        Assert.Contains("--projectPath", error.Message, StringComparison.Ordinal);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + DaemonTimeouts.ProbeAttemptTimeoutCap,
            timeProvider.GetUtcNow());
    }

    private static IpcResponse CreateResponse (Guid requestId)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcResponseStatus.Ok,
            payload: JsonDocument.Parse("{}").RootElement.Clone(),
            errors: Array.Empty<IpcError>());
    }

    private static IpcResponse CreateSessionTokenInvalidResponse (Guid requestId)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            status: IpcResponseStatus.Error,
            payload: JsonDocument.Parse("{}").RootElement.Clone(),
            errors:
            [
                new IpcError(
                    IpcSessionErrorCodes.SessionTokenInvalid,
                    "Session token is invalid.",
                    OpId: null),
            ]);
    }

    private static RecordingIpcTransportClient CreateTransportClient ()
    {
        return new RecordingIpcTransportClient(static request => CreateResponse(request.RequestId));
    }

    private static DaemonSessionReadResult CreateSessionReadResult (string sessionToken)
    {
        return DaemonSessionReadResultTestFactory.FoundForToken(sessionToken);
    }

    private static DaemonSession CreateGuiSession (
        string sessionToken,
        Guid sessionGenerationId)
    {
        return DaemonSessionTestFactory.Create(
            sessionToken: sessionToken,
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli-session.sock",
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId,
            sessionGenerationId: sessionGenerationId);
    }

    private static DaemonLifecycleObservation CreateRecoveringObservation (DaemonSession session)
    {
        return new DaemonLifecycleObservation(
            processId: session.ProcessId!.Value,
            processStartedAtUtc: session.ProcessStartedAtUtc!.Value,
            state: new UnityEditorStateSnapshot(
                editorMode: session.EditorMode,
                lifecycleState: IpcEditorLifecycleState.DomainReloading,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(1, 2, 0, 0),
                playMode: new IpcPlayModeSnapshot(
                    IpcPlayModeState.Stopped,
                    IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: DateTimeOffset.UtcNow,
            actionRequired: null,
            primaryDiagnostic: null,
            serverVersion: null,
            editorInstanceId: session.EditorInstanceId
                ?? throw new ArgumentException("Session must have an Editor instance identifier.", nameof(session)),
            recoveryLease: null);
    }

    private static async Task ObserveCompletionAsync (Task task)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(1)));
        if (ReferenceEquals(completedTask, task))
        {
            _ = task.Exception;
        }
    }

    private sealed class TimeAdvancingDaemonSessionStore : ReadOnlyDaemonSessionStore
    {
        private readonly ManualTimeProvider timeProvider;

        private readonly TimeSpan elapsed;

        private readonly DaemonSessionReadResult result;

        public TimeAdvancingDaemonSessionStore (
            ManualTimeProvider timeProvider,
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

        private readonly bool blockCancellationCallback;

        private readonly TaskCompletionSource<bool> startedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationCallbackStartedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationCallbackReleaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> resolutionReleaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingDaemonSessionStore (
            DaemonSessionReadResult result,
            bool blockCancellationCallback)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            this.blockCancellationCallback = blockCancellationCallback;
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
            using var cancellationRegistration = blockCancellationCallback
                ? cancellationToken.Register(() =>
                {
                    cancellationCallbackStartedSource.TrySetResult(true);
                    cancellationCallbackReleaseSource.Task.GetAwaiter().GetResult();
                })
                : default;
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
