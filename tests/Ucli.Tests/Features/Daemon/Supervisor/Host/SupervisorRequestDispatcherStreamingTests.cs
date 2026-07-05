using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorRequestDispatcherTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestDispatcherStreamingTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEnsureRunningStreamEmitsProgress_WritesProgressBeforeTerminal ()
    {
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            canShutdownProcess: false,
            endpointTransportKind: "unixDomainSocket",
            endpointAddress: "/tmp/ucli.sock",
            processId: 42,
            ownerProcessId: 24);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = DaemonStartResult.Started(session),
            OnStart = async (progressObserver, cancellationToken) =>
            {
                Assert.NotNull(progressObserver);
                await progressObserver!.EmitWaitingForEndpointAsync(
                        new DaemonStartStartupProgressObservation(
                            LaunchAttemptId: "attempt-1",
                            EditorMode: "batchmode",
                            OwnerKind: "cli",
                            CanShutdownProcess: false,
                            ProcessId: 42,
                            ProcessStartedAtUtc: session.ProcessStartedAtUtc,
                            StartupStatus: "waitingForEndpoint",
                            StartupBlockingReason: null,
                            StartupPhase: "endpointRegistration",
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
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-stream-progress",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: "batchmode",
                        OnStartupBlocked: "auto")),
                responseMode: IpcResponseMode.Stream));

        Assert.Equal(2, frames.Count);
        Assert.Equal(IpcStreamFrameKinds.Progress, frames[0].Kind);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint), frames[0].Event);
        JsonAssert.For(frames[0].Payload)
            .HasString("payloadKind", "startupObservation")
            .HasString("projectFingerprint", projectFingerprint)
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
        Assert.Equal(session, terminalPayload.Session);
        DaemonStartOperationAssert.EnsureRunningStreamRequested(
            startOperation,
            runtimeContext.StorageRoot,
            unityProjectRoot,
            projectFingerprint,
            TimeSpan.FromMilliseconds(1000),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEnsureRunningStreamProgressWriteFails_CancelsStartOperation ()
    {
        var progressWriteCanceled = false;
        var startOperation = new RecordingDaemonStartOperation
        {
            OnStart = async (progressObserver, cancellationToken) =>
            {
                Assert.NotNull(progressObserver);
                try
                {
                    await progressObserver!.EmitWaitingForEndpointAsync(
                            new DaemonStartStartupProgressObservation(
                                LaunchAttemptId: "attempt-1",
                                EditorMode: "batchmode",
                                OwnerKind: "cli",
                                CanShutdownProcess: true,
                                ProcessId: 42,
                                ProcessStartedAtUtc: DateTimeOffset.UtcNow,
                                StartupStatus: "waitingForEndpoint",
                                StartupBlockingReason: null,
                                StartupPhase: "endpointRegistration",
                                RetryDisposition: null,
                                Message: null,
                                ErrorCode: null),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    progressWriteCanceled = true;
                    throw;
                }
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
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-stream-write-failure",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: "batchmode",
                        OnStartupBlocked: "auto")),
                responseMode: IpcResponseMode.Stream));

        Assert.True(progressWriteCanceled);
        DaemonStartOperationAssert.EnsureRunningStreamRequested(
            startOperation,
            runtimeContext.StorageRoot,
            unityProjectRoot,
            projectFingerprint,
            TimeSpan.FromMilliseconds(1000),
            DaemonEditorMode.Batchmode,
            DaemonStartupBlockedProcessPolicy.Auto);
        var terminalFrame = Assert.Single(frames);
        Assert.Equal(IpcStreamFrameKinds.Terminal, terminalFrame.Kind);
        var terminalResponse = Assert.IsType<IpcResponse>(terminalFrame.Response);
        Assert.Equal(IpcProtocol.StatusError, terminalResponse.Status);
        var error = Assert.Single(terminalResponse.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Contains("caller disconnected", error.Message, StringComparison.Ordinal);
    }
}
