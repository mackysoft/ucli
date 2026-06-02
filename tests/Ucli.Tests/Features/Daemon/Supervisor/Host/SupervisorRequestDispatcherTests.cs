using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Project;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestDispatcherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenSessionTokenIsMissing_ReturnsSessionTokenRequired ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-missing-token",
                SessionToken: string.Empty,
                Method: SupervisorIpcContracts.PingMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcSessionErrorCodes.SessionTokenRequired, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenSessionTokenIsInvalid_ReturnsSessionTokenInvalid ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-invalid-token",
                SessionToken: "invalid-token",
                Method: SupervisorIpcContracts.PingMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcSessionErrorCodes.SessionTokenInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenResponseModeIsUnsupported_ReturnsInvalidArgumentWithoutStartOperation ()
    {
        var startOperation = new StubDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-unsupported-response-mode",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                ResponseMode: "unsupported"));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("Unsupported IPC response mode: unsupported.", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, startOperation.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenResponseModeIsNull_ReturnsInvalidArgumentWithNullPlaceholder ()
    {
        var startOperation = new StubDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-null-response-mode",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                ResponseMode: null!));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("Unsupported IPC response mode: <null>.", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, startOperation.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenResponseModeIsMissing_ReturnsInvalidArgumentWithNullPlaceholder ()
    {
        var startOperation = new StubDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);
        var payload = IpcPayloadCodec.SerializeToElement(
            new SupervisorIpcContracts.EnsureRunningRequest(
                UnityProjectRoot: unityProjectRoot,
                ProjectFingerprint: projectFingerprint,
                TimeoutMilliseconds: 1000,
                EditorMode: null,
                OnStartupBlocked: "auto"));
        var rawRequest = JsonSerializer.SerializeToElement(
            new
            {
                ProtocolVersion = IpcProtocol.CurrentVersion,
                RequestId = "request-missing-response-mode",
                SessionToken = runtimeContext.Manifest.SessionToken,
                Method = SupervisorIpcContracts.EnsureRunningMethod,
                Payload = payload,
            },
            IpcJsonSerializerOptions.Default);

        var response = await SendRawJsonRequestAsync(dispatcher, runtimeContext, rawRequest);

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("Unsupported IPC response mode: <null>.", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, startOperation.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenStreamResponseModeTargetsPing_ReturnsInvalidArgument ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();

        var frames = await SendStreamingRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-ping-stream-response-mode",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.PingMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: IpcResponseMode.Stream));

        var terminalFrame = Assert.Single(frames);
        Assert.Equal(IpcStreamFrameKinds.Terminal, terminalFrame.Kind);
        var response = Assert.IsType<IpcResponse>(terminalFrame.Response);
        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains(SupervisorIpcContracts.EnsureRunningMethod, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenUnityProjectRootIsInvalid_ReturnsInvalidArgumentWithoutBreakingSubsequentRequests ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();

        var invalidResponse = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-1",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: "bad\u0000path",
                        ProjectFingerprint: "fingerprint",
                        TimeoutMilliseconds: 1000,
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusError, invalidResponse.Status);
        var invalidError = Assert.Single(invalidResponse.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, invalidError.Code);

        var pingResponse = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-2",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.PingMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusOk, pingResponse.Status);
        Assert.Empty(pingResponse.Errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenProjectFingerprintDoesNotMatchUnityProjectRoot_ReturnsInvalidArgument ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-mismatch",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: "mismatched-fingerprint",
                        TimeoutMilliseconds: 1000,
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("Project fingerprint does not match", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEditorModeIsSpecified_PassesNormalizedValueToStartOperation ()
    {
        var lifecycleSnapshot = new DaemonStartLifecycleSnapshot(
            IpcEditorLifecycleStateCodec.Compiling,
            IpcEditorBlockingReasonCodec.Compile,
            CanAcceptExecutionRequests: false);
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(CreateSession(), lifecycleSnapshot),
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-editor-mode",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: " gui ",
                        OnStartupBlocked: " terminate ")),
                responseMode: IpcResponseMode.Single));

        Assert.True(
            string.Equals(IpcProtocol.StatusOk, response.Status, StringComparison.Ordinal),
            string.Join(Environment.NewLine, response.Errors.Select(error => $"{error.Code.Value}: {error.Message}")));
        Assert.Equal(DaemonEditorMode.Gui, startOperation.LastEditorMode);
        Assert.Equal(DaemonStartupBlockedProcessPolicy.Terminate, startOperation.LastOnStartupBlocked);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningResponse payload,
            out _));
        Assert.Equal(lifecycleSnapshot, payload.LifecycleSnapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenStartOperationAttaches_EmitsAttachedStartStatus ()
    {
        var session = CreateSession(canShutdownProcess: false);
        var lifecycleSnapshot = new DaemonStartLifecycleSnapshot(
            IpcEditorLifecycleStateCodec.Ready,
            null,
            CanAcceptExecutionRequests: true);
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.Attached(session, lifecycleSnapshot),
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-attached",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: "gui",
                        OnStartupBlocked: "auto")),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusOk, response.Status);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningResponse payload,
            out _));
        Assert.Equal("attached", payload.StartStatus);
        Assert.Equal(session, payload.Session);
        Assert.Equal(lifecycleSnapshot, payload.LifecycleSnapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEditorModeIsInvalid_ReturnsInvalidArgumentWithoutStartOperation ()
    {
        var startOperation = new StubDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-invalid-editor-mode",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: "unsupported",
                        OnStartupBlocked: "auto")),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Equal(0, startOperation.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenOnStartupBlockedIsInvalid_ReturnsInvalidArgumentWithoutStartOperation ()
    {
        var startOperation = new StubDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-invalid-startup-policy",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: null,
                        OnStartupBlocked: "unsupported")),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Equal(0, startOperation.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEnsureRunningFailsWithDiagnosisAndStartup_EmitsFailureMetadataPayload ()
    {
        var diagnosis = CreateDiagnosis();
        var startup = CreateStartupObservation();
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.Failure(
                ExecutionError.Timeout("endpoint registration timed out", ExecutionErrorCodes.IpcTimeout),
                diagnosis,
                startup,
                DaemonStatusKind.Stale),
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-diagnosis",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningFailureResponse payload,
            out _));
        Assert.Equal(diagnosis, payload.Diagnosis);
        Assert.Equal(startup, payload.Startup);
        Assert.Equal("stale", payload.DaemonStatus);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEnsureRunningFailureCompletesAfterPayloadTimeout_EmitsDiagnosisPayload ()
    {
        var diagnosis = CreateDiagnosis();
        var startOperation = new StubDaemonStartOperation
        {
            DelayBeforeResult = TimeSpan.FromMilliseconds(25),
            StartResult = DaemonStartResult.Failure(
                ExecutionError.Timeout("endpoint registration timed out", ExecutionErrorCodes.IpcTimeout),
                diagnosis),
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-delayed-diagnosis",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1,
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningFailureResponse payload,
            out _));
        Assert.Equal(diagnosis, payload.Diagnosis);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEnsureRunningStreamEmitsProgress_WritesProgressBeforeTerminal ()
    {
        var session = CreateSession(canShutdownProcess: false);
        var startOperation = new StubDaemonStartOperation
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
        Assert.NotNull(startOperation.LastProgressObserver);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEnsureRunningStreamProgressWriteFails_CancelsStartOperation ()
    {
        var progressWriteCanceled = false;
        var startOperation = new StubDaemonStartOperation
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
        Assert.NotNull(startOperation.LastProgressObserver);
        var terminalFrame = Assert.Single(frames);
        Assert.Equal(IpcStreamFrameKinds.Terminal, terminalFrame.Kind);
        var terminalResponse = Assert.IsType<IpcResponse>(terminalFrame.Response);
        Assert.Equal(IpcProtocol.StatusError, terminalResponse.Status);
        var error = Assert.Single(terminalResponse.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Contains("caller disconnected", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenCallerDisconnectsDuringEnsureRunning_ReturnsIpcTimeout ()
    {
        var startOperation = new StubDaemonStartOperation
        {
            WaitUntilCancellation = true,
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestWithCallerDisconnectAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-caller-disconnect",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.EnsureRunningMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        TimeoutMilliseconds: 1000,
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Contains("caller disconnected", error.Message, StringComparison.Ordinal);
    }

    private static SupervisorRequestDispatcher CreateDispatcher (StubDaemonStartOperation? startOperation = null)
    {
        var activityTracker = new SupervisorActivityTracker();
        var diagnosisStore = new StubDaemonDiagnosisStore();
        var runtimeLogger = new SupervisorRuntimeLogger();
        var coordinator = new SupervisorProjectCoordinator(
            startOperation ?? new StubDaemonStartOperation(),
            new StubDaemonStopOperation(),
            new StubDaemonPingClient(),
            new DaemonReachabilityClassifier(),
            new SupervisorStabilityVerifier(
                new StubDaemonPingClient(),
                new SupervisorDiagnosisWriter(diagnosisStore)),
            new SupervisorExitHandler(
                new StubDaemonSessionStore(),
                new StubDaemonArtifactCleaner(),
                new SupervisorDiagnosisWriter(diagnosisStore),
                runtimeLogger),
            runtimeLogger);
        return new SupervisorRequestDispatcher(activityTracker, coordinator);
    }

    private static SupervisorRuntimeContext CreateRuntimeContext ()
    {
        return new SupervisorRuntimeContext(
            StorageRoot: Path.Combine(Path.GetTempPath(), "ucli-dispatcher-tests", Guid.NewGuid().ToString("N")),
            Manifest: new SupervisorInstanceManifest(
                ProcessId: 1234,
                SessionToken: "supervisor-session-token",
                EndpointTransportKind: "unixDomainSocket",
                EndpointAddress: "/tmp/ucli-supervisor-test.sock",
                IssuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero)));
    }

    private static async Task<IpcResponse> SendRequestAsync (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        IpcRequest request)
    {
        return await SendFramedRequestAsync(dispatcher, runtimeContext, request).ConfigureAwait(false);
    }

    private static async Task<IpcResponse> SendRawJsonRequestAsync (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        JsonElement request)
    {
        return await SendFramedRequestAsync(dispatcher, runtimeContext, request).ConfigureAwait(false);
    }

    private static async Task<IpcResponse> SendFramedRequestAsync<TRequest> (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        TRequest request)
    {
        using var stream = new NonDisconnectingMemoryStream();
        await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
        var requestLength = stream.Length;
        stream.Position = 0;

        await dispatcher.HandleConnectionAsync(stream, runtimeContext, CancellationToken.None).ConfigureAwait(false);

        stream.Position = requestLength;
        return await IpcFrameCodec.ReadModelAsync<IpcResponse>(
                stream,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
    }

    private static async Task<IpcResponse> SendRequestWithCallerDisconnectAsync (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        IpcRequest request)
    {
        using var stream = new CallerDisconnectingMemoryStream();
        await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
        var requestLength = stream.Length;
        stream.Position = 0;

        await dispatcher.HandleConnectionAsync(stream, runtimeContext, CancellationToken.None).ConfigureAwait(false);

        stream.Position = requestLength;
        return await IpcFrameCodec.ReadModelAsync<IpcResponse>(
                stream,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<IpcStreamFrame>> SendStreamingRequestAsync (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        IpcRequest request)
    {
        using var stream = new DuplexMemoryStream(await CreateRequestFrameBytesAsync(request).ConfigureAwait(false));

        await dispatcher.HandleConnectionAsync(stream, runtimeContext, CancellationToken.None).ConfigureAwait(false);

        using var outputStream = new MemoryStream(stream.GetWrittenBytes());
        var frames = new List<IpcStreamFrame>();
        while (true)
        {
            var frame = await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
                    outputStream,
                    IpcJsonSerializerOptions.Default)
                .ConfigureAwait(false);
            frames.Add(frame);
            if (string.Equals(frame.Kind, IpcStreamFrameKinds.Terminal, StringComparison.Ordinal))
            {
                return frames;
            }
        }
    }

    private static async Task<IReadOnlyList<IpcStreamFrame>> SendStreamingRequestWithTransientWriteFailureAsync (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        IpcRequest request)
    {
        using var stream = new DuplexMemoryStream(await CreateRequestFrameBytesAsync(request).ConfigureAwait(false))
        {
            FailWriteCount = 1,
        };

        await dispatcher.HandleConnectionAsync(stream, runtimeContext, CancellationToken.None).ConfigureAwait(false);

        using var outputStream = new MemoryStream(stream.GetWrittenBytes());
        var frames = new List<IpcStreamFrame>();
        while (outputStream.Position < outputStream.Length)
        {
            frames.Add(await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
                    outputStream,
                    IpcJsonSerializerOptions.Default)
                .ConfigureAwait(false));
        }

        return frames;
    }

    private static async Task<byte[]> CreateRequestFrameBytesAsync (IpcRequest request)
    {
        using var stream = new MemoryStream();
        await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
        return stream.ToArray();
    }

    private class NonDisconnectingMemoryStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync (
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (Position < Length)
            {
                return await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return 0;
        }
    }

    private sealed class CallerDisconnectingMemoryStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync (
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (Position < Length)
            {
                return await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }

            return 0;
        }
    }

    private sealed class DuplexMemoryStream : Stream
    {
        private readonly byte[] input;
        private readonly MemoryStream output = new();
        private int inputOffset;

        public DuplexMemoryStream (byte[] input)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public bool FailWrites { get; set; }

        public int FailWriteCount { get; set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public byte[] GetWrittenBytes ()
        {
            return output.ToArray();
        }

        public override void Flush ()
        {
        }

        public override int Read (
            byte[] buffer,
            int offset,
            int count)
        {
            if (inputOffset >= input.Length)
            {
                return 0;
            }

            var bytesRead = Math.Min(count, input.Length - inputOffset);
            input.AsSpan(inputOffset, bytesRead).CopyTo(buffer.AsSpan(offset, bytesRead));
            inputOffset += bytesRead;
            return bytesRead;
        }

        public override async ValueTask<int> ReadAsync (
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (inputOffset < input.Length)
            {
                var bytesRead = Math.Min(buffer.Length, input.Length - inputOffset);
                input.AsMemory(inputOffset, bytesRead).CopyTo(buffer);
                inputOffset += bytesRead;
                return bytesRead;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override long Seek (
            long offset,
            SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength (long value)
        {
            throw new NotSupportedException();
        }

        public override void Write (
            byte[] buffer,
            int offset,
            int count)
        {
            if (ShouldFailWrite())
            {
                throw new IOException("Simulated caller disconnect.");
            }

            output.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync (
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (ShouldFailWrite())
            {
                throw new IOException("Simulated caller disconnect.");
            }

            output.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        private bool ShouldFailWrite ()
        {
            if (FailWrites)
            {
                return true;
            }

            if (FailWriteCount <= 0)
            {
                return false;
            }

            FailWriteCount--;
            return true;
        }
    }

    private sealed class StubDaemonStartOperation : IDaemonStartOperation
    {
        public DaemonStartResult StartResult { get; set; } = DaemonStartResult.AlreadyRunning(CreateSession());

        public TimeSpan DelayBeforeResult { get; set; }

        public bool WaitUntilCancellation { get; set; }

        public int CallCount { get; private set; }

        public DaemonEditorMode? LastEditorMode { get; private set; }

        public DaemonStartupBlockedProcessPolicy LastOnStartupBlocked { get; private set; }

        public IDaemonStartProgressObserver? LastProgressObserver { get; private set; }

        public Func<IDaemonStartProgressObserver?, CancellationToken, ValueTask>? OnStart { get; set; }

        public async ValueTask<DaemonStartResult> StartAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            DaemonEditorMode? editorMode,
            DaemonStartupBlockedProcessPolicy onStartupBlocked,
            IDaemonStartProgressObserver? progressObserver = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastEditorMode = editorMode;
            LastOnStartupBlocked = onStartupBlocked;
            LastProgressObserver = progressObserver;
            if (OnStart != null)
            {
                await OnStart(progressObserver, cancellationToken).ConfigureAwait(false);
            }

            if (WaitUntilCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }

            if (DelayBeforeResult > TimeSpan.Zero)
            {
                await Task.Delay(DelayBeforeResult, cancellationToken).ConfigureAwait(false);
            }

            return StartResult;
        }
    }

    private sealed class StubDaemonStopOperation : IDaemonStopOperation
    {
        public ValueTask<DaemonStopResult> StopAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonStopResult.Stopped());
        }
    }

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        public ValueTask PingAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubDaemonDiagnosisStore : IDaemonDiagnosisStore
    {
        public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public ValueTask<DaemonSessionReadResult> ReadAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionReadResult.Success(null));
        }

        public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }

        public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonArtifactCleaner : IDaemonArtifactCleaner
    {
        public ValueTask<DaemonArtifactCleanupResult> CleanupAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonArtifactCleanupResult.Success());
        }
    }

    private static DaemonSession CreateSession (bool canShutdownProcess = true)
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            EditorMode: "batchmode",
            OwnerKind: "cli",
            CanShutdownProcess: canShutdownProcess,
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli.sock",
            ProcessId: 42,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 24);
    }

    private static DaemonDiagnosis CreateDiagnosis ()
    {
        return new DaemonDiagnosis(
            Reason: DaemonDiagnosisReasonValues.GuiEndpointNotRegistered,
            Message: "GUI endpoint not registered.",
            ReportedBy: DaemonDiagnosisReportedByValues.Cli,
            IsInferred: true,
            UpdatedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 3, 0, TimeSpan.Zero),
            ProcessId: 1234,
            EditorInstancePath: "/repo/UnityProject/Library/EditorInstance.json",
            SessionIssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 2, 0, TimeSpan.Zero));
    }

    private static DaemonStartupObservation CreateStartupObservation ()
    {
        return new DaemonStartupObservation(
            StartupStatus: ContractLiteralCodec.ToValue(DaemonStartupStatus.Blocked),
            StartupBlockingReason: ContractLiteralCodec.ToValue(DaemonStartupBlockingReason.Compile),
            LaunchAttemptId: null,
            ProcessAction: ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Kept),
            RetryDisposition: ContractLiteralCodec.ToValue(DaemonStartupRetryDisposition.RetryAfterFix));
    }
}
