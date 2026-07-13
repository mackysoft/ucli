using MackySoft.Tests;
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
                            LaunchAttemptId: "attempt-1",
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
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var frames = await SendStreamingRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        DeadlineUtc: CreateEnsureRunningDeadline(1000),
                        AttemptTimeoutMilliseconds: 1000,
                        EditorMode: "gui",
                        OnStartupBlocked: "auto")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream)));

        Assert.Equal(2, frames.Count);
        Assert.Equal(IpcStreamFrameKinds.Progress, frames[0].Kind);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint), frames[0].Event);
        JsonAssert.For(frames[0].Payload)
            .HasString("payloadKind", "startupObservation")
            .HasString("projectFingerprint", projectFingerprint.ToString())
            .HasInt32("timeoutMilliseconds", 1000)
            .HasString("message", "Waiting for daemon endpoint.");
        Assert.Equal(IpcStreamFrameKinds.Terminal, frames[1].Kind);
        Assert.Null(frames[1].Event);
        var terminalResponse = Assert.IsType<IpcResponse>(frames[1].Response);
        Assert.True(
            string.Equals(IpcProtocol.StatusOk, terminalResponse.Status, StringComparison.Ordinal),
            string.Join(Environment.NewLine, terminalResponse.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        Assert.True(IpcPayloadCodec.TryDeserialize(
            terminalResponse.Payload,
            out SupervisorIpcContracts.EnsureRunningResponse terminalPayload,
            out _));
        Assert.Equal("started", terminalPayload.StartStatus);
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
                            LaunchAttemptId: "attempt-1",
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
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var frames = await SendStreamingRequestWithTransientWriteFailureAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        DeadlineUtc: CreateEnsureRunningDeadline(1000),
                        AttemptTimeoutMilliseconds: 1000,
                        EditorMode: "batchmode",
                        OnStartupBlocked: "auto")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream)));

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
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);
        var request = new IpcRequest(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
            method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
            payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.EnsureRunningRequest(
                    UnityProjectRoot: unityProjectRoot,
                    ProjectFingerprint: projectFingerprint,
                    DeadlineUtc: CreateEnsureRunningDeadline(1000),
                    AttemptTimeoutMilliseconds: 1000,
                    EditorMode: "batchmode",
                    OnStartupBlocked: "auto")),
            responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single));

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
                            LaunchAttemptId: "attempt-blocking-cancellation",
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
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var sendTask = SendStreamingRequestWithTransientWriteFailureAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        DeadlineUtc: CreateEnsureRunningDeadline(1000),
                        AttemptTimeoutMilliseconds: 1000,
                        EditorMode: "batchmode",
                        OnStartupBlocked: "auto")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream)));
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
