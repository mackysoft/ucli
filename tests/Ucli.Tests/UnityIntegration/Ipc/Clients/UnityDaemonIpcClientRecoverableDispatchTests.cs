using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientRecoverableDispatchTests
{
    private static readonly DateTimeOffset HostProcessStartedAtUtc =
        new(2026, 3, 5, 0, 0, 1, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenStatelessReadResponseIsInterrupted_ReplaysSameLogicalRequest ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new IpcResponseReadInterruptedException(
            new EndOfStreamException("lost stateless read response")));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var interruptedSession = CreateHostSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UnityEditorMode.Batchmode);
        var differentHostSession = CreateMismatchedHostSession(
            interruptedSession,
            HostIdentityMismatchKind.ProcessId);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    DaemonSessionReadResultTestFactory.Found(interruptedSession),
                    DaemonSessionReadResultTestFactory.Found(differentHostSession))));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            new UnityIpcDispatchRequest(
                UnityIpcMethod.OpsRead,
                CreateDispatchPayload(),
                UnityBatchmodeLaunchOptions.Default),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.OpsRead,
            UnityIpcMethod.OpsRead);
        _ = IpcRequestAssert.SingleRequestId(requests);
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(HostIdentityMismatchKind.ProcessId)]
    [InlineData(HostIdentityMismatchKind.ProcessStartedAtUtc)]
    [InlineData(HostIdentityMismatchKind.EditorInstanceId)]
    public async Task SendAsync_WhenDurableReplayFindsDifferentHost_PreservesInterruptionWithoutReplay (
        HostIdentityMismatchKind mismatchKind)
    {
        var interruption = new IpcResponseReadInterruptedException(
            new EndOfStreamException("original durable response interruption"));
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(interruption);
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var interruptedSession = CreateHostSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            mismatchKind == HostIdentityMismatchKind.EditorInstanceId
                ? UnityEditorMode.Gui
                : UnityEditorMode.Batchmode);
        var successorSession = CreateMismatchedHostSession(interruptedSession, mismatchKind);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    DaemonSessionReadResultTestFactory.Found(interruptedSession),
                    DaemonSessionReadResultTestFactory.Found(successorSession))));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateLifecycleDispatchRequest(LifecycleExecutionKind.PlayEnter),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, result.FailureInfo!.FailureKind);
        Assert.Equal(EditorLifecycleErrorCodes.EditorUnavailable, result.ErrorCode);
        Assert.Contains(interruption.Message, result.Message, StringComparison.Ordinal);
        Assert.Single(IpcRequestAssert.ActionRequests(transportClient.Requests));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(DurableReacquisitionTrigger.PreWriteFailure)]
    [InlineData(DurableReacquisitionTrigger.SessionTokenRejection)]
    [InlineData(DurableReacquisitionTrigger.ResponseInterruption)]
    [InlineData(DurableReacquisitionTrigger.TerminalTransportFailure)]
    public async Task SendAsync_WhenDurableReplayRequiresAnotherReacquisition_PreservesFirstHostAndInterruption (
        DurableReacquisitionTrigger trigger)
    {
        var firstInterruption = new IpcResponseReadInterruptedException(
            new EndOfStreamException("first durable response interruption"));
        var secondInterruption = new IpcResponseReadInterruptedException(
            new EndOfStreamException("second durable response interruption"));
        var terminalTransportFailure = new InvalidDataException(
            "terminal replay transport failure");
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(_ => throw firstInterruption);
        transportClient.EnqueueResponse(trigger switch
        {
            DurableReacquisitionTrigger.PreWriteFailure => _ => throw new IpcConnectException(
                "IPC connection was refused before the replay request was sent.",
                new SocketException((int)SocketError.ConnectionRefused)),
            DurableReacquisitionTrigger.SessionTokenRejection => _ => CreateSessionTokenInvalidResponse(),
            DurableReacquisitionTrigger.ResponseInterruption => _ => throw secondInterruption,
            DurableReacquisitionTrigger.TerminalTransportFailure => _ => throw terminalTransportFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null),
        });
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var interruptedSession = CreateHostSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UnityEditorMode.Batchmode);
        var sameHostSuccessor = CreateHostSession(
            "daemon-token-2",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UnityEditorMode.Batchmode);
        var differentHostSuccessor = CreateMismatchedHostSession(
            interruptedSession,
            HostIdentityMismatchKind.ProcessId);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    DaemonSessionReadResultTestFactory.Found(interruptedSession),
                    DaemonSessionReadResultTestFactory.Found(sameHostSuccessor),
                    DaemonSessionReadResultTestFactory.Found(differentHostSuccessor))));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateLifecycleDispatchRequest(LifecycleExecutionKind.PlayEnter),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, result.FailureInfo!.FailureKind);
        Assert.Equal(
            trigger == DurableReacquisitionTrigger.TerminalTransportFailure
                ? UcliCoreErrorCodes.InternalError
                : EditorLifecycleErrorCodes.EditorUnavailable,
            result.ErrorCode);
        Assert.Contains(firstInterruption.Message, result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secondInterruption.Message, result.Message, StringComparison.Ordinal);
        if (trigger == DurableReacquisitionTrigger.TerminalTransportFailure)
        {
            Assert.Contains(terminalTransportFailure.Message, result.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain(terminalTransportFailure.Message, result.Message, StringComparison.Ordinal);
        }
        var actionRequests = IpcRequestAssert.ActionRequests(transportClient.Requests);
        Assert.Equal(2, actionRequests.Count);
        _ = IpcRequestAssert.SingleRequestId(actionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenDurableReplayHasPreWriteFailure_DoesNotExtendEndpointAvailabilityWindow ()
    {
        var timeProvider = new ManualTimeProvider();
        var firstInterruption = new IpcResponseReadInterruptedException(
            new EndOfStreamException("durable response interruption before endpoint deadline"));
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(_ => throw firstInterruption);
        transportClient.EnqueueResponse(_ =>
        {
            timeProvider.Advance(
                DaemonTimeouts.ProbeAttemptTimeoutCap
                - TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
            throw new IpcConnectException(
                "IPC connection was refused before the replay request was sent.",
                new SocketException((int)SocketError.ConnectionRefused));
        });
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var interruptedSession = CreateHostSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UnityEditorMode.Batchmode);
        var sameHostSuccessor = CreateHostSession(
            "daemon-token-2",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UnityEditorMode.Batchmode);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    DaemonSessionReadResultTestFactory.Found(interruptedSession),
                    DaemonSessionReadResultTestFactory.Found(sameHostSuccessor))));

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
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(sendTask.IsCompleted);
        timeProvider.Advance(retryDelay);
        var result = await sendTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, result.FailureInfo!.FailureKind);
        Assert.Equal(EditorLifecycleErrorCodes.EditorUnavailable, result.ErrorCode);
        Assert.Contains(firstInterruption.Message, result.Message, StringComparison.Ordinal);
        Assert.Equal(
            DateTimeOffset.UnixEpoch + DaemonTimeouts.ProbeAttemptTimeoutCap,
            timeProvider.GetUtcNow());
        var actionRequests = IpcRequestAssert.ActionRequests(transportClient.Requests);
        Assert.Equal(2, actionRequests.Count);
        _ = IpcRequestAssert.SingleRequestId(actionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenResponseInterruptionExhaustsCompletionDeadline_ReturnsTransportTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var executionTimeout = TimeSpan.FromSeconds(5);
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(_ =>
        {
            timeProvider.Advance(
                executionTimeout
                + LifecycleExecutionTiming.ResponseDeliveryGrace);
            throw new IpcResponseReadInterruptedException(
                new EndOfStreamException(
                    "response interruption at completion deadline"));
        });
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(CreateSessionReadResult("daemon-token-1"))));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
                CreateLifecycleDispatchRequest(
                    LifecycleExecutionKind.PlayEnter,
                    timeProvider,
                    executionTimeout),
                ExecutionDeadline.Start(executionTimeout, timeProvider),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, result.FailureInfo!.FailureKind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Contains(
            "response interruption at completion deadline",
            result.Message,
            StringComparison.Ordinal);
        Assert.Single(IpcRequestAssert.ActionRequests(transportClient.Requests));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSuccessorWaitExhaustsCompletionDeadline_ReturnsTransportTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var executionTimeout = TimeSpan.FromSeconds(5);
        var completionTimeout =
            executionTimeout
            + LifecycleExecutionTiming.ResponseDeliveryGrace;
        var interruption = new IpcResponseReadInterruptedException(
            new EndOfStreamException("response interruption before successor wait"));
        var transportClient = new RecordingIpcTransportClient(_ => throw interruption);
        var interruptedSession = CreateHostSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UnityEditorMode.Gui);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    DaemonSessionReadResultTestFactory.Found(interruptedSession),
                    DaemonSessionReadResult.Missing()),
                CreateRecoveryWaiter(
                    interruptedSession,
                    timeProvider,
                    completionTimeout)));

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                CreateLifecycleDispatchRequest(
                    LifecycleExecutionKind.PlayEnter,
                    timeProvider,
                    executionTimeout),
                ExecutionDeadline.Start(executionTimeout, timeProvider),
                CancellationToken.None)
            .AsTask();

        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            sendTask,
            completionTimeout,
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await sendTask;

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, result.FailureInfo!.FailureKind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Contains(interruption.Message, result.Message, StringComparison.Ordinal);
        Assert.Single(IpcRequestAssert.ActionRequests(transportClient.Requests));
        Assert.Equal(
            DateTimeOffset.UnixEpoch + completionTimeout,
            timeProvider.GetUtcNow());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenResponseReplayCannotReadSession_ReturnsTransportUnavailable ()
    {
        var interruption = new IpcResponseReadInterruptedException(
            new EndOfStreamException("response interruption before invalid session metadata"));
        var transportClient = new RecordingIpcTransportClient(_ => throw interruption);
        var interruptedSession = CreateHostSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UnityEditorMode.Batchmode);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    DaemonSessionReadResultTestFactory.Found(interruptedSession),
                    DaemonSessionReadResultTestFactory.Invalid())));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateLifecycleDispatchRequest(LifecycleExecutionKind.PlayEnter),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, result.FailureInfo!.FailureKind);
        Assert.Equal(EditorLifecycleErrorCodes.EditorUnavailable, result.ErrorCode);
        Assert.Contains("Synthetic invalid daemon session", result.Message, StringComparison.Ordinal);
        Assert.Contains(interruption.Message, result.Message, StringComparison.Ordinal);
        Assert.Single(IpcRequestAssert.ActionRequests(transportClient.Requests));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenReplaySessionTokenIsRejectedWithoutSuccessor_ReturnsTransportUnavailable ()
    {
        var timeProvider = new ManualTimeProvider();
        var interruption = new IpcResponseReadInterruptedException(
            new EndOfStreamException("response interruption before rejected replay token"));
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(interruption);
        transportClient.EnqueueResponse(CreateSessionTokenInvalidResponse());
        var interruptedSession = CreateHostSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UnityEditorMode.Batchmode);
        var sameHostSuccessor = CreateHostSession(
            "daemon-token-2",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UnityEditorMode.Batchmode);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    DaemonSessionReadResultTestFactory.Found(interruptedSession),
                    DaemonSessionReadResultTestFactory.Found(sameHostSuccessor),
                    DaemonSessionReadResultTestFactory.Found(sameHostSuccessor))));

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                CreateLifecycleDispatchRequest(
                    LifecycleExecutionKind.PlayEnter,
                    timeProvider,
                    TimeSpan.FromSeconds(5)),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();

        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            sendTask,
            DaemonTimeouts.SessionPublicationRetryTimeout,
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await sendTask;

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityRequestFailureKind.TransportInterrupted, result.FailureInfo!.FailureKind);
        Assert.Equal(EditorLifecycleErrorCodes.EditorUnavailable, result.ErrorCode);
        Assert.Contains("No successor daemon session was published", result.Message, StringComparison.Ordinal);
        Assert.Contains(interruption.Message, result.Message, StringComparison.Ordinal);
        var actionRequests = IpcRequestAssert.ActionRequests(transportClient.Requests);
        Assert.Equal(2, actionRequests.Count);
        _ = IpcRequestAssert.SingleRequestId(actionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverablePlayResponseIsInterrupted_PreservesCompletionDeadlineAcrossRetry ()
    {
        var timeProvider = new ManualTimeProvider();
        var executionTimeout = TimeSpan.FromSeconds(5);
        var completionTimeout =
            executionTimeout
            + LifecycleExecutionTiming.ResponseDeliveryGrace;
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new IpcResponseReadInterruptedException(
            new EndOfStreamException("lost play transition response")));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));
        var registration = UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
            LifecycleExecutionKind.PlayEnter,
            timeProvider: timeProvider,
            executionTimeout: executionTimeout);
        var dispatchRequest = new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.PlayEnter(
                registration,
                requiredStart: null));

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                dispatchRequest,
                ExecutionDeadline.Start(executionTimeout, timeProvider),
                CancellationToken.None)
            .AsTask();

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.PlayEnter,
            UnityIpcMethod.PlayEnter);
        var actionTimeouts = transportClient.Requests
            .Zip(transportClient.Timeouts)
            .Where(pair => string.Equals(
                pair.First.Method,
                TextVocabulary.GetText(UnityIpcMethod.PlayEnter),
                StringComparison.Ordinal))
            .Select(static pair => pair.Second)
            .ToArray();
        Assert.Equal(2, actionTimeouts.Length);
        Assert.Equal(completionTimeout, actionTimeouts[0]);
        Assert.True(actionTimeouts[1] < actionTimeouts[0]);
        Assert.True(
            actionTimeouts[1]
            > completionTimeout - TimeSpan.FromSeconds(1));
        _ = IpcRequestAssert.SingleRequestId(requests);
        Assert.All(
            requests,
            request => Assert.Equal(
                DateTimeOffset.UnixEpoch + completionTimeout,
                request.RequestDeadlineUtc));
        Assert.All(requests, request => Assert.True(request.RequestDeadlineRemainingMilliseconds > 1000));
        Assert.True(
            requests[1].RequestDeadlineRemainingMilliseconds
            < requests[0].RequestDeadlineRemainingMilliseconds);
        Assert.Equal(requests[0].Method, requests[1].Method);
        Assert.Equal(requests[0].Payload.GetRawText(), requests[1].Payload.GetRawText());
        Assert.All(requests, request => Assert.False(request.Payload.TryGetProperty("timeoutMilliseconds", out _)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenDispatchUsesRotatedSessionToken_RejectsDifferentHostAfterStart ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(CreateSessionTokenInvalidResponse());
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var rejectedSession = CreateHostSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UnityEditorMode.Batchmode);
        var differentHostSession = CreateMismatchedHostSession(
            rejectedSession,
            HostIdentityMismatchKind.ProcessId);
        var sessionStore = new QueuedDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(rejectedSession),
            DaemonSessionReadResultTestFactory.Found(differentHostSession));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateLifecycleDispatchRequest(LifecycleExecutionKind.PlayEnter),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.HostMismatch,
            result.ErrorCode);
        var actionRequest = Assert.Single(
            IpcRequestAssert.ActionRequests(
                transportClient.Requests));
        Assert.Equal(
            IpcSessionTokenTestFactory.Create("daemon-token-1")
                .GetEncodedValue(),
            actionRequest.SessionToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRotatedSessionTokenAttemptLosesRecoverableResponse_RetriesSameRequest ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(CreateSessionTokenInvalidResponse());
        transportClient.EnqueueResponse(static _ => throw new IpcResponseReadInterruptedException(
            new EndOfStreamException("lost response after session token rotation")));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                CreateLifecycleDispatchRequest(
                    LifecycleExecutionKind.PlayEnter,
                    timeProvider,
                    TimeSpan.FromSeconds(5)),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();

        await ManualTimeTaskDriver.AdvanceUntilCompletedAsync(
            timeProvider,
            sendTask,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds));
        var result = await sendTask;

        Assert.True(result.IsSuccess);
        var requests = IpcRequestAssert.Methods(
            transportClient,
            UnityIpcMethod.PlayEnter,
            UnityIpcMethod.PlayEnter,
            UnityIpcMethod.PlayEnter);
        IpcRequestAssert.SessionTokens(
            requests,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        _ = IpcRequestAssert.SingleRequestId(requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenRecoverableDispatchLosesResponse_RetriesWithSameRequestIdAndReloadedSessionToken ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new IpcResponseReadInterruptedException(
            new EndOfStreamException("lost response")));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token-1"),
            CreateSessionReadResult("daemon-token-2"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateLifecycleDispatchRequest(LifecycleExecutionKind.PlayEnter),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var requests = DaemonIpcDispatchAssert.RecoveredDispatchesWithReloadedSessionToken(
            transportClient,
            UnityIpcMethod.PlayEnter,
            IpcSessionTokenTestFactory.Create("daemon-token-1").GetEncodedValue(),
            IpcSessionTokenTestFactory.Create("daemon-token-2").GetEncodedValue());
        var requestId = IpcRequestAssert.SingleRequestId(requests);
        Assert.NotEqual(Guid.Empty, requestId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSuccessorStartResponseIsInvalid_RetainsThePreviouslyConfirmedExecution ()
    {
        var interruption = new IpcResponseReadInterruptedException(
            new EndOfStreamException("lost lifecycle action response"));
        var startAttempt = 0;
        var transportClient = new RecordingIpcTransportClient(
            request =>
            {
                return IpcRequestAssert.ParseMethod(request) switch
                {
                    UnityIpcMethod.LifecycleStart when ++startAttempt == 1 =>
                        LifecycleExecutionIpcTestResponseFactory.TryCreateResponse(request)!,
                    UnityIpcMethod.LifecycleStart =>
                        CreateResponse(request.RequestId),
                    UnityIpcMethod.PlayEnter => throw interruption,
                    _ => throw new Xunit.Sdk.XunitException(
                        $"Unexpected method: {request.Method}"),
                };
            },
            createLifecycleStartResponses: false);
        var interruptedSession = CreateHostSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UnityEditorMode.Batchmode);
        var successorSession = CreateHostSession(
            "daemon-token-2",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UnityEditorMode.Batchmode);
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                new QueuedDaemonSessionStore(
                    DaemonSessionReadResultTestFactory.Found(interruptedSession),
                    DaemonSessionReadResultTestFactory.Found(successorSession))));
        var dispatchRequest = CreateLifecycleDispatchRequest(
            LifecycleExecutionKind.PlayEnter);

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            dispatchRequest,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal(
            dispatchRequest.Registration!.ExecutionId,
            result.LifecycleExecutionStart!.LifecycleExecutionRef.Id);
        Assert.Equal(
            2,
            IpcRequestAssert.WithMethod(
                transportClient,
                UnityIpcMethod.LifecycleStart).Count);
        Assert.Single(IpcRequestAssert.ActionRequests(transportClient.Requests));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenPreWriteConnectionIsRefused_RejectsDifferentHostAfterStart ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueException(new IpcConnectException(
            "IPC connection was refused before the request was sent.",
            new SocketException((int)SocketError.ConnectionRefused)));
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var failedSession = CreateHostSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UnityEditorMode.Batchmode);
        var differentHostSession = CreateMismatchedHostSession(
            failedSession,
            HostIdentityMismatchKind.ProcessId);
        var sessionStore = new QueuedDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(failedSession),
            DaemonSessionReadResultTestFactory.Found(differentHostSession));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateLifecycleDispatchRequest(LifecycleExecutionKind.Compile),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(5), TimeProvider.System),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.HostMismatch,
            result.ErrorCode);
        var actionRequest = Assert.Single(
            IpcRequestAssert.ActionRequests(
                transportClient.Requests));
        Assert.Equal(
            IpcSessionTokenTestFactory.Create("daemon-token-1")
                .GetEncodedValue(),
            actionRequest.SessionToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenLifecycleDispatchConnectionRefusalOutlivesEndpointAbsenceGrace_ReturnsEditorUnavailable ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => throw new IpcConnectException(
            "IPC connection was refused before the request was sent.",
            new SocketException((int)SocketError.ConnectionRefused)));
        var sessionStore = new QueuedDaemonSessionStore(
            CreateSessionReadResult("daemon-token"));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore));

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                CreateLifecycleDispatchRequest(
                    LifecycleExecutionKind.Compile,
                    timeProvider,
                    TimeSpan.FromSeconds(5)),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(DaemonTimeouts.ProbeAttemptTimeoutCap - retryDelay);

        var result = await sendTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(result.IsSuccess);
        Assert.Equal(
            EditorLifecycleErrorCodes.EditorUnavailable,
            result.ErrorCode);
        DaemonIpcDispatchAssert.RetriedDispatchesWithSameRequestId(
            transportClient,
            UnityIpcMethod.Compile,
            maximumAttempts: 19);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionTokenRecoveryPublishesDifferentHost_RejectsRecoveredSession ()
    {
        var timeProvider = new ManualTimeProvider();
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse(Guid.NewGuid()));
        transportClient.EnqueueResponse(CreateSessionTokenInvalidResponse());
        transportClient.EnqueueResponse(CreateResponse(Guid.NewGuid()));
        var rejectedSession = CreateHostSession(
            "daemon-token-1",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UnityEditorMode.Gui);
        var recoveredDifferentHostSession = CreateMismatchedHostSession(
            rejectedSession,
            HostIdentityMismatchKind.ProcessId);
        var recoveryWaiter = CreateRecoveryWaiter(rejectedSession, timeProvider);
        var sessionStore = new QueuedDaemonSessionStore(
            DaemonSessionReadResultTestFactory.Found(rejectedSession),
            DaemonSessionReadResult.Missing(),
            DaemonSessionReadResultTestFactory.Found(recoveredDifferentHostSession));
        var client = new UnityDaemonIpcClient(
            transportClient,
            DaemonSessionAcquisitionCoordinatorTestFactory.Create(
                sessionStore,
            recoveryWaiter));

        var sendTask = client.SendAsync(
                ResolvedUnityProjectContextTestFactory.Create(),
                CreateLifecycleDispatchRequest(
                    LifecycleExecutionKind.PlayEnter,
                    timeProvider,
                    TimeSpan.FromSeconds(5)),
                ExecutionDeadline.Start(TimeSpan.FromSeconds(5), timeProvider),
                CancellationToken.None)
            .AsTask();
        Assert.False(sendTask.IsCompleted);

        var retryDelay = TimeSpan.FromMilliseconds(DaemonTimeouts.StartupProbeRetryDelayMilliseconds);
        await timeProvider.WaitForTimerDueWithinAsync(retryDelay).WaitAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(retryDelay);
        var result = await sendTask;

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.HostMismatch,
            result.ErrorCode);
        var actionRequest = Assert.Single(
            IpcRequestAssert.ActionRequests(
                transportClient.Requests));
        Assert.Equal(
            IpcSessionTokenTestFactory.Create("daemon-token-1")
                .GetEncodedValue(),
            actionRequest.SessionToken);
    }

    private static DaemonSession CreateHostSession (
        string sessionToken,
        Guid sessionGenerationId,
        UnityEditorMode editorMode)
    {
        var isGui = editorMode == UnityEditorMode.Gui;
        return DaemonSessionTestFactory.Create(
            processId: 1234,
            sessionToken: sessionToken,
            processStartedAtUtc: HostProcessStartedAtUtc,
            editorMode: editorMode,
            ownerKind: isGui ? DaemonSessionOwnerKind.User : DaemonSessionOwnerKind.Cli,
            canShutdownProcess: !isGui,
            editorInstanceId: isGui
                ? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
                : null,
            sessionGenerationId: sessionGenerationId);
    }

    private static DaemonSession CreateMismatchedHostSession (
        DaemonSession interruptedSession,
        HostIdentityMismatchKind mismatchKind)
    {
        return DaemonSessionTestFactory.Create(
            processId: mismatchKind == HostIdentityMismatchKind.ProcessId
                ? 5678
                : interruptedSession.ProcessId,
            sessionToken: "daemon-token-2",
            projectFingerprint: interruptedSession.ProjectFingerprint,
            processStartedAtUtc: mismatchKind == HostIdentityMismatchKind.ProcessStartedAtUtc
                ? HostProcessStartedAtUtc.AddSeconds(10)
                : interruptedSession.ProcessStartedAtUtc,
            editorMode: interruptedSession.EditorMode,
            ownerKind: interruptedSession.OwnerKind,
            canShutdownProcess: interruptedSession.CanShutdownProcess,
            editorInstanceId: mismatchKind == HostIdentityMismatchKind.EditorInstanceId
                ? Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
                : interruptedSession.EditorInstanceId,
            sessionGenerationId: Guid.Parse("33333333-3333-3333-3333-333333333333"));
    }

    public enum DurableReacquisitionTrigger
    {
        PreWriteFailure,
        SessionTokenRejection,
        ResponseInterruption,
        TerminalTransportFailure,
    }

    public enum HostIdentityMismatchKind
    {
        ProcessId,
        ProcessStartedAtUtc,
        EditorInstanceId,
    }
}
