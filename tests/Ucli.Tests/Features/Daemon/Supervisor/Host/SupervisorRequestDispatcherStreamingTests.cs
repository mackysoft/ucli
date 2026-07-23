using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorRequestDispatcherTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestDispatcherStreamingTests
{
    private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEnsureRunningStreamEmitsProgress_WritesProgressBeforeTerminal ()
    {
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            editorMode: DaemonEditorMode.Gui,
            ownerKind: DaemonSessionOwnerKind.User,
            canShutdownProcess: false,
            endpointTransportKind: IpcTransportKind.UnixDomainSocket,
            endpointAddress: "/tmp/ucli.sock",
            processId: 42,
            ownerProcessId: 24,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = DaemonStartResult.Started(
                session,
                IpcUnityEditorObservationTestFactory.Create(projectFingerprint: session.ProjectFingerprint)),
            OnStart = async (progressObserver, cancellationToken) =>
            {
                Assert.NotNull(progressObserver);
                await progressObserver!.EmitWaitingForEndpointAsync(
                        new DaemonStartStartupProgressObservation(
                            LaunchAttemptId: Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
                            EditorMode: DaemonEditorMode.Gui,
                            OwnerKind: DaemonSessionOwnerKind.User,
                            CanShutdownProcess: false,
                            ProcessId: 42,
                            ProcessStartedAtUtc: session.ProcessStartedAtUtc,
                            StartupStatus: DaemonStartupStatus.WaitingForEndpoint,
                            StartupBlockingReason: null,
                            StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
                            RetryDisposition: null,
                            Message: "Waiting for daemon endpoint.",
                            ErrorCode: null),
                        cancellationToken)
                    .ConfigureAwait(false);
            },
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = AbsolutePath.Parse(Path.Combine(runtimeContext.StorageRoot.Value, "UnityProject"));
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var frames = await SendStreamingRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot.Value,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: DaemonEditorMode.Gui,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

        Assert.Equal(2, frames.Count);
        Assert.Equal(IpcStreamFrameKind.Progress, frames[0].Kind);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint), frames[0].Event);
        var operationTimeoutMilliseconds = checked(
            (int)Math.Ceiling(Assert.Single(startOperation.Invocations).RemainingTimeout.TotalMilliseconds));
        JsonAssert.For(frames[0].Payload)
            .HasString("payloadKind", "startupObservation")
            .HasString("projectFingerprint", projectFingerprint.ToString())
            .HasInt32("timeoutMilliseconds", operationTimeoutMilliseconds)
            .HasString("message", "Waiting for daemon endpoint.");
        Assert.Equal(IpcStreamFrameKind.Terminal, frames[1].Kind);
        Assert.Null(frames[1].Event);
        var terminalResponse = Assert.IsType<IpcResponse>(frames[1].Response);
        Assert.True(
            terminalResponse.Status == IpcResponseStatus.Ok,
            string.Join(Environment.NewLine, terminalResponse.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        Assert.True(IpcPayloadCodec.TryDeserialize(
            terminalResponse.Payload,
            out SupervisorIpcContracts.EnsureRunningResponse terminalPayload,
            out _));
        Assert.Equal(DaemonStartStatus.Started, terminalPayload.StartStatus);
        Assert.Equal(DaemonSessionContractMapper.ToContract(session), terminalPayload.Session);
        DaemonStartOperationAssert.EnsureRunningStreamRequested(
            startOperation,
            runtimeContext.StorageRoot,
            unityProjectRoot,
            projectFingerprint,
            TimeSpan.FromMilliseconds(1000),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Auto);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenProgressIsEmittedAfterResponseFrameWriteTimeout_KeepsRequestStreamOpen ()
    {
        var startEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var emitProgress = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeProvider = new ManualTimeProvider();
        var startOperation = new RecordingDaemonStartOperation
        {
            OnStart = async (progressObserver, cancellationToken) =>
            {
                startEntered.TrySetResult();
                await emitProgress.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                await progressObserver!.EmitWaitingForEndpointAsync(
                        new DaemonStartStartupProgressObservation(
                            LaunchAttemptId: Guid.Parse("11234567-89ab-cdef-0123-456789abcdef"),
                            EditorMode: DaemonEditorMode.Batchmode,
                            OwnerKind: DaemonSessionOwnerKind.Cli,
                            CanShutdownProcess: true,
                            ProcessId: 42,
                            ProcessStartedAtUtc: timeProvider.GetUtcNow(),
                            StartupStatus: DaemonStartupStatus.WaitingForEndpoint,
                            StartupBlockingReason: null,
                            StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
                            RetryDisposition: null,
                            Message: "Still starting.",
                            ErrorCode: null),
                        cancellationToken)
                    .ConfigureAwait(false);
            },
        };
        var dispatcher = CreateDispatcher(startOperation, timeProvider);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = AbsolutePath.Parse(Path.Combine(runtimeContext.StorageRoot.Value, "UnityProject"));
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);
        var sendTask = SendStreamingRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot.Value,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: DaemonEditorMode.Batchmode,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream),
                requestDeadlineUtc: timeProvider.GetUtcNow().AddSeconds(5),
                requestDeadlineRemainingMilliseconds: 5000));
        await TestAwaiter.WaitAsync(
            startEntered.Task,
            "Supervisor start operation",
            SignalWaitTimeout);

        timeProvider.Advance(TimeSpan.FromSeconds(2));
        emitProgress.TrySetResult();
        var frames = await sendTask;

        Assert.Equal(2, frames.Count);
        Assert.Equal(IpcStreamFrameKind.Progress, frames[0].Kind);
        Assert.Equal(IpcStreamFrameKind.Terminal, frames[1].Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEnsureRunningStreamProgressWriteFails_CancelsStartOperationAndSealsStream ()
    {
        var progressWriteCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationRegistration = default(CancellationTokenRegistration);
        var startOperation = new RecordingDaemonStartOperation
        {
            OnStart = async (progressObserver, cancellationToken) =>
            {
                Assert.NotNull(progressObserver);
                cancellationRegistration = cancellationToken.Register(
                    () => progressWriteCancellationObserved.TrySetResult());
                await progressObserver!.EmitWaitingForEndpointAsync(
                        new DaemonStartStartupProgressObservation(
                            LaunchAttemptId: Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
                            EditorMode: DaemonEditorMode.Batchmode,
                            OwnerKind: DaemonSessionOwnerKind.Cli,
                            CanShutdownProcess: true,
                            ProcessId: 42,
                            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
                            StartupStatus: DaemonStartupStatus.WaitingForEndpoint,
                            StartupBlockingReason: null,
                            StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
                            RetryDisposition: null,
                            Message: null,
                            ErrorCode: null),
                        cancellationToken)
                    .ConfigureAwait(false);
            },
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = AbsolutePath.Parse(Path.Combine(runtimeContext.StorageRoot.Value, "UnityProject"));
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var frames = await SendStreamingRequestWithTransientWriteFailureAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot.Value,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: DaemonEditorMode.Batchmode,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

        await TestAwaiter.WaitAsync(
            progressWriteCancellationObserved.Task,
            "Supervisor progress-write cancellation",
            SignalWaitTimeout);
        cancellationRegistration.Dispose();
        DaemonStartOperationAssert.EnsureRunningStreamRequested(
            startOperation,
            runtimeContext.StorageRoot,
            unityProjectRoot,
            projectFingerprint,
            TimeSpan.FromMilliseconds(1000),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto);
        Assert.Empty(frames);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenStartOperationHasUnrelatedCancellation_DoesNotClassifyCallerDisconnect ()
    {
        var expectedException = new OperationCanceledException(
            "unrelated start cancellation",
            new ArgumentException("not a connection write failure"),
            CancellationToken.None);
        var startOperation = new RecordingDaemonStartOperation
        {
            OnStart = (_, _) => throw expectedException,
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = AbsolutePath.Parse(Path.Combine(runtimeContext.StorageRoot.Value, "UnityProject"));
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);
        var request = new IpcRequestEnvelope(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
            method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
            payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.EnsureRunningRequest(
                    UnityProjectRoot: unityProjectRoot.Value,
                    ProjectFingerprint: projectFingerprint,
                    EditorMode: DaemonEditorMode.Batchmode,
                    OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
            responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
            requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
            requestDeadlineRemainingMilliseconds: 1000);

        var actualException = await Assert.ThrowsAsync<OperationCanceledException>(
            () => SendRequestAsync(dispatcher, runtimeContext, request));

        Assert.Same(expectedException, actualException);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenProgressWriteFailureRunsBlockingCancellationCallback_ReturnsWithoutWaitingForCallback ()
    {
        using var releaseCancellationCallback = new ManualResetEventSlim();
        var cancellationCallbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationRegistration = default(CancellationTokenRegistration);
        var startOperation = new RecordingDaemonStartOperation
        {
            OnStart = async (progressObserver, cancellationToken) =>
            {
                Assert.NotNull(progressObserver);
                cancellationRegistration = cancellationToken.Register(() =>
                {
                    cancellationCallbackEntered.TrySetResult();
                    releaseCancellationCallback.Wait();
                });
                await progressObserver!.EmitWaitingForEndpointAsync(
                        new DaemonStartStartupProgressObservation(
                            LaunchAttemptId: Guid.Parse("21234567-89ab-cdef-0123-456789abcdef"),
                            EditorMode: DaemonEditorMode.Batchmode,
                            OwnerKind: DaemonSessionOwnerKind.Cli,
                            CanShutdownProcess: true,
                            ProcessId: 42,
                            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
                            StartupStatus: DaemonStartupStatus.WaitingForEndpoint,
                            StartupBlockingReason: null,
                            StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
                            RetryDisposition: null,
                            Message: null,
                            ErrorCode: null),
                        cancellationToken)
                    .ConfigureAwait(false);
            },
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = AbsolutePath.Parse(Path.Combine(runtimeContext.StorageRoot.Value, "UnityProject"));
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var sendTask = SendStreamingRequestWithTransientWriteFailureAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot.Value,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: DaemonEditorMode.Batchmode,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));
        await TestAwaiter.WaitAsync(
            cancellationCallbackEntered.Task,
            "Supervisor blocking cancellation callback",
            SignalWaitTimeout);

        IReadOnlyList<IpcStreamFrame> frames;
        var returnedWithoutWaiting = false;
        try
        {
            frames = await sendTask.WaitAsync(SignalWaitTimeout);
            returnedWithoutWaiting = true;
        }
        finally
        {
            releaseCancellationCallback.Set();
            frames = await sendTask.WaitAsync(SignalWaitTimeout);
            cancellationRegistration.Dispose();
        }

        Assert.True(returnedWithoutWaiting);
        Assert.Empty(frames);
    }
}
