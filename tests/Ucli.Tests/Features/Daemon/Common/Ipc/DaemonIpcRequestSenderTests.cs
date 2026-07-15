using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
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
            new StaticDaemonSessionConnectionProvider(DaemonSessionConnectionResolutionResult.SessionNotAvailable()),
            recoveryWaiter: null,
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenResponseAttemptTimesOut_ReturnsTimeoutWithoutRetry ()
    {
        var transportClient = CreateTransportClient();
        transportClient.EnqueueException(new TimeoutException("attempt timed out"));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            new StaticDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token")),
            recoveryWaiter: null,
            TimeProvider.System);

        var result = await sender.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            UnityIpcMethod.UnityLogsRead,
            RequestPayload,
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        DaemonIpcDispatchAssert.SingleDispatchPreservedCallerTimeoutBudget(
            transportClient,
            expectedMethod: UnityIpcMethod.UnityLogsRead,
            expectedSessionToken: IpcSessionTokenTestFactory.Create("daemon-token").GetEncodedValue(),
            minimumTimeout: TimeSpan.FromSeconds(4.9));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendAsync_WhenSessionResolutionDoesNotQuiesce_ReturnsTimeoutAtDeadline (
        bool blockCancellationCallback)
    {
        var timeProvider = new ManualTimeProvider();
        var sessionConnectionProvider = new BlockingSessionConnectionProvider(
            CreateConnectionResult("daemon-token"),
            blockCancellationCallback);
        var sender = new DaemonIpcRequestSender(
            new UnexpectedIpcTransportClient("Timed-out session resolution must stop before transport dispatch."),
            sessionConnectionProvider,
            recoveryWaiter: null,
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
            await sessionConnectionProvider.Started.WaitAsync(TimeSpan.FromSeconds(1));
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));

            timeProvider.Advance(timeout);
            if (blockCancellationCallback)
            {
                await sessionConnectionProvider.CancellationCallbackStarted.WaitAsync(TimeSpan.FromSeconds(1));
            }

            var completedTask = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(sendTask, completedTask);
            var result = await sendTask;
            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, Assert.IsType<ExecutionError>(result.Error).Kind);
        }
        finally
        {
            sessionConnectionProvider.ReleaseCancellationCallback();
            sessionConnectionProvider.ReleaseResolution();
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
            new TimeAdvancingSessionConnectionProvider(
                timeProvider,
                TimeSpan.FromSeconds(2),
                CreateConnectionResult("daemon-token")),
            recoveryWaiter: null,
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
    public async Task SendAsync_WhenRecoveryReadConsumesDeadline_ReturnsTimeoutWithoutAdditionalRetryDelay ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueException(new IpcConnectException(
            "IPC connection was refused before the request was sent.",
            new SocketException((int)SocketError.ConnectionRefused)));
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
        var sender = new DaemonIpcRequestSender(
            transportClient,
            new StaticDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token")),
            recoveryWaiter,
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
            await readStartedSource.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await timeProvider
                .WaitForTimerDueWithinAsync(timeout)
                .WaitAsync(TimeSpan.FromSeconds(1));

            timeProvider.Advance(timeout);

            var completedTask = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.Same(sendTask, completedTask);
            var result = await sendTask;
            Assert.False(result.IsSuccess);
            Assert.Equal(ExecutionErrorKind.Timeout, Assert.IsType<ExecutionError>(result.Error).Kind);
            Assert.Single(transportClient.Requests);
        }
        finally
        {
            readReleaseSource.TrySetResult(true);
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
        var session = DaemonSessionTestFactory.CreateEditorInstance();
        var recoveryWaiter = new UnityDaemonRecoveryWaiter(
            new RecordingDaemonSessionStore
            {
                ReadResult = DaemonSessionReadResultTestFactory.Found(session),
            },
            new RecordingDaemonLifecycleStore
            {
                ReadResult = DaemonLifecycleObservationReadResult.Success(CreateRecoveringObservation(session)),
            },
            new RecordingDaemonProcessIdentityAssessor(DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token-1"),
            CreateConnectionResult("daemon-token-2"));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            sessionConnectionProvider,
            recoveryWaiter,
            timeProvider);

        var sendTask = sender.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                UnityIpcMethod.UnityLogsRead,
                RequestPayload,
                TimeSpan.FromSeconds(5),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
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
        Assert.True(
            requests[0].RequestDeadlineRemainingMilliseconds
            > requests[1].RequestDeadlineRemainingMilliseconds);
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
            new QueuedDaemonSessionConnectionProvider(
                CreateConnectionResult("daemon-token-1"),
                CreateConnectionResult("daemon-token-2")),
            recoveryWaiter: null,
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
        timeProvider.Advance(TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));

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
        Assert.True(
            requests[0].RequestDeadlineRemainingMilliseconds
            > requests[1].RequestDeadlineRemainingMilliseconds);
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
            new QueuedDaemonSessionConnectionProvider(
                CreateConnectionResult("daemon-token-1"),
                CreateConnectionResult("daemon-token-1"),
                CreateConnectionResult("daemon-token-1"),
                CreateConnectionResult("daemon-token-2"),
                CreateConnectionResult("daemon-token-3")),
            recoveryWaiter: null,
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
    public async Task SendAsync_WhenSuccessorSessionTokenIsRejected_DoesNotRetryThirdSession ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = CreateTransportClient();
        transportClient.EnqueueResponse(static request => CreateSessionTokenInvalidResponse(request.RequestId));
        transportClient.EnqueueResponse(static request => CreateSessionTokenInvalidResponse(request.RequestId));
        transportClient.EnqueueResponse(static request => CreateResponse(request.RequestId));
        var sender = new DaemonIpcRequestSender(
            transportClient,
            new QueuedDaemonSessionConnectionProvider(
                CreateConnectionResult("daemon-token-1"),
                CreateConnectionResult("daemon-token-2"),
                CreateConnectionResult("daemon-token-3")),
            recoveryWaiter: null,
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
        Assert.Equal(IpcResponseStatus.Error, result.Response!.Status);
        Assert.Equal(IpcSessionErrorCodes.SessionTokenInvalid, Assert.Single(result.Response.Errors).Code);
        var requests = IpcRequestAssert.Methods(transportClient, UnityIpcMethod.UnityLogsRead, UnityIpcMethod.UnityLogsRead);
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSuccessorReplayIsRefused_DoesNotReplayWithThirdSessionToken ()
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
            new QueuedDaemonSessionConnectionProvider(
                CreateConnectionResult("daemon-token-1"),
                CreateConnectionResult("daemon-token-2"),
                CreateConnectionResult("daemon-token-3")),
            recoveryWaiter: null,
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

        Assert.False(result.IsSuccess);
        Assert.Equal(
            DaemonErrorCodes.DaemonSessionNotAvailable,
            Assert.IsType<ExecutionError>(result.Error).Code);
        var requests = IpcRequestAssert.Methods(transportClient, UnityIpcMethod.UnityLogsRead, UnityIpcMethod.UnityLogsRead);
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
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
            new StaticDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token")),
            recoveryWaiter: null,
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
            new StaticDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token")),
            recoveryWaiter: null,
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
            ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
                    timeProvider,
                    sendTask,
                    DaemonTimeouts.ProbeAttemptTimeoutCap + retryDelay,
                    retryDelay)
                .AsTask(),
            "Endpoint absence daemon IPC manual-time drive",
            AsyncWaitTimeout);

        var result = await sendTask;

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

    private static DaemonSessionConnectionResolutionResult CreateConnectionResult (string sessionToken)
    {
        return DaemonSessionConnectionResolutionResult.Success(new DaemonSessionConnection(
            IpcSessionTokenTestFactory.Create(sessionToken),
            new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli-session.sock")));
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

    private sealed class TimeAdvancingSessionConnectionProvider : IDaemonSessionConnectionProvider
    {
        private readonly ManualTimeProvider timeProvider;

        private readonly TimeSpan elapsed;

        private readonly DaemonSessionConnectionResolutionResult result;

        public TimeAdvancingSessionConnectionProvider (
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

    private sealed class BlockingSessionConnectionProvider : IDaemonSessionConnectionProvider
    {
        private readonly DaemonSessionConnectionResolutionResult result;

        private readonly bool blockCancellationCallback;

        private readonly TaskCompletionSource<bool> startedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationCallbackStartedSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> cancellationCallbackReleaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> resolutionReleaseSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingSessionConnectionProvider (
            DaemonSessionConnectionResolutionResult result,
            bool blockCancellationCallback)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            this.blockCancellationCallback = blockCancellationCallback;
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
