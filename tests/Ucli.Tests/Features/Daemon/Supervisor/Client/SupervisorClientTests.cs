using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenNamedPipeConnectTimesOutAndProcessIsDead_ReturnsUnreachable ()
    {
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new IpcConnectTimeoutException("connect timeout"),
        };
        var client = new SupervisorClient(transportClient);
        var manifest = new SupervisorInstanceManifest(
            ProcessId: int.MaxValue,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-supervisor-test",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));

        var result = await client.ProbeReachabilityAsync(
            manifest,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.Equal(SupervisorReachabilityProbeStatus.Unreachable, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenNamedPipeConnectTimesOutAndProcessIsAlive_ReturnsTimedOut ()
    {
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new IpcConnectTimeoutException("connect timeout"),
        };
        var client = new SupervisorClient(transportClient);
        var manifest = new SupervisorInstanceManifest(
            ProcessId: Environment.ProcessId,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-supervisor-test",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));

        var result = await client.ProbeReachabilityAsync(
            manifest,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.Equal(SupervisorReachabilityProbeStatus.TimedOut, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_UsesOriginalOperationTimeoutAndUnboundedResponseWait ()
    {
        var observedOperationTimeoutMilliseconds = 0;
        var observedOnStartupBlocked = (string?)null;
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (endpoint, request, timeout, cancellationToken) =>
            {
                Assert.True(IpcPayloadCodec.TryDeserialize(
                    request.Payload,
                    out SupervisorIpcContracts.EnsureRunningRequest payload,
                    out _));
                observedOperationTimeoutMilliseconds = payload.TimeoutMilliseconds;
                observedOnStartupBlocked = payload.OnStartupBlocked;

                return ValueTask.FromResult(new IpcResponse(
                    ProtocolVersion: request.ProtocolVersion,
                    RequestId: request.RequestId,
                    Status: IpcProtocol.StatusOk,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningResponse(
                        StartStatus: "started",
                        DaemonStatus: "running",
                        Session: CreateSession(),
                        LifecycleSnapshot: new DaemonStartLifecycleSnapshot(
                            IpcEditorLifecycleStateCodec.Compiling,
                            IpcEditorBlockingReasonCodec.Compile,
                            CanAcceptExecutionRequests: false))),
                    Errors: []));
            },
        };
        var client = new SupervisorClient(transportClient);
        var requestedTimeout = TimeSpan.FromSeconds(5);

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            requestedTimeout,
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Terminate,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(IpcEditorLifecycleStateCodec.Compiling, result.LifecycleSnapshot!.LifecycleState);
        Assert.Equal(IpcEditorBlockingReasonCodec.Compile, result.LifecycleSnapshot.BlockingReason);
        Assert.False(result.LifecycleSnapshot.CanAcceptExecutionRequests);
        var call = Assert.Single(transportClient.Calls);
        Assert.True(call.UsesUnboundedResponseWait);
        Assert.Equal(requestedTimeout, call.Timeout);
        Assert.Equal((int)requestedTimeout.TotalMilliseconds, observedOperationTimeoutMilliseconds);
        Assert.Equal("terminate", observedOnStartupBlocked);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithProgressObserver_UsesStreamResponseModeAndForwardsProgressFrame ()
    {
        var transportClient = new StreamingTransportClient((request, onProgressFrame, cancellationToken) =>
        {
            Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), request.ResponseMode);

            var progressPayload = new DaemonStartStartupObservationProgressEntry(
                ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation),
                "fingerprint",
                5000,
                "gui",
                "terminate",
                "attempt-1",
                "cli",
                true,
                42,
                new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero),
                "waitingForEndpoint",
                null,
                "endpointRegistration",
                null,
                "Waiting for daemon endpoint.",
                null);
            var progressFrame = new IpcStreamFrame(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcStreamFrameKinds.Progress,
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint),
                IpcPayloadCodec.SerializeToElement(progressPayload),
                Response: null);
            return InvokeProgressAndCreateResponseAsync(
                request,
                onProgressFrame,
                progressFrame,
                cancellationToken);
        });
        var progressSink = new CollectingProgressSink();
        var client = new SupervisorClient(transportClient);

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Terminate,
            progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var call = Assert.Single(transportClient.StreamingCalls);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), call.Request.ResponseMode);
        Assert.True(call.UsesUnboundedResponseWait);
        Assert.Equal(TimeSpan.FromSeconds(5), call.Timeout);
        var progress = Assert.Single(progressSink.Entries);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint), progress.EventName);
        var payload = Assert.IsType<DaemonStartStartupObservationProgressEntry>(progress.Payload);
        Assert.Equal("startupObservation", payload.PayloadKind);
        Assert.Equal("fingerprint", payload.ProjectFingerprint);
        Assert.Equal("Waiting for daemon endpoint.", payload.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithProgressObserver_WhenTerminalFails_PreservesFailureMetadata ()
    {
        var diagnosis = CreateDiagnosis();
        var startup = CreateStartupObservation();
        var transportClient = new StreamingTransportClient((request, onProgressFrame, cancellationToken) => ValueTask.FromResult(new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusError,
            Payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.EnsureRunningFailureResponse("stale", diagnosis, startup)),
            Errors:
            [
                new IpcError(ExecutionErrorCodes.IpcTimeout, "endpoint registration timed out", null),
            ])));
        var progressSink = new CollectingProgressSink();
        var client = new SupervisorClient(transportClient);

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Equal(startup, result.Startup);
        Assert.Equal(DaemonStatusKind.Stale, result.DaemonStatus);
        Assert.Empty(progressSink.Entries);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), Assert.Single(transportClient.StreamingCalls).Request.ResponseMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithProgressSink_ForwardsLifecycleSnapshotProgressFrame ()
    {
        var transportClient = new StreamingTransportClient((request, onProgressFrame, cancellationToken) =>
        {
            var progressPayload = new DaemonStartLifecycleSnapshotProgressEntry(
                ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.LifecycleSnapshot),
                "fingerprint",
                5000,
                "gui",
                "auto",
                IpcEditorLifecycleStateCodec.Compiling,
                IpcEditorBlockingReasonCodec.Compile,
                CanAcceptExecutionRequests: false);
            var progressFrame = new IpcStreamFrame(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcStreamFrameKinds.Progress,
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.LifecycleObserved),
                IpcPayloadCodec.SerializeToElement(progressPayload),
                Response: null);
            return InvokeProgressAndCreateResponseAsync(
                request,
                onProgressFrame,
                progressFrame,
                cancellationToken);
        });
        var progressSink = new CollectingProgressSink();
        var client = new SupervisorClient(transportClient);

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var progress = Assert.Single(progressSink.Entries);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartProgressEvent.LifecycleObserved), progress.EventName);
        var payload = Assert.IsType<DaemonStartLifecycleSnapshotProgressEntry>(progress.Payload);
        Assert.Equal("lifecycleSnapshot", payload.PayloadKind);
        Assert.Equal(IpcEditorLifecycleStateCodec.Compiling, payload.LifecycleState);
        Assert.Equal(IpcEditorBlockingReasonCodec.Compile, payload.BlockingReason);
        Assert.False(payload.CanAcceptExecutionRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithProgressSink_WhenProgressEventIsUnsupported_DropsProgressAndPreservesTerminal ()
    {
        var transportClient = new StreamingTransportClient((request, onProgressFrame, cancellationToken) =>
        {
            var progressFrame = new IpcStreamFrame(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcStreamFrameKinds.Progress,
                "daemon.start.unknown",
                IpcPayloadCodec.SerializeToElement(new { payloadKind = "startupObservation" }),
                Response: null);
            return InvokeProgressAndCreateResponseAsync(
                request,
                onProgressFrame,
                progressFrame,
                cancellationToken);
        });
        var client = new SupervisorClient(transportClient);
        var progressSink = new CollectingProgressSink();

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(progressSink.Entries);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithProgressSink_WhenPayloadKindDoesNotMatchEvent_DropsProgressAndPreservesTerminal ()
    {
        var transportClient = new StreamingTransportClient((request, onProgressFrame, cancellationToken) =>
        {
            var progressPayload = new DaemonStartStartupObservationProgressEntry(
                ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.LifecycleSnapshot),
                "fingerprint",
                5000,
                "gui",
                "auto",
                null,
                "user",
                false,
                42,
                null,
                "waitingForEndpoint",
                null,
                "endpointRegistration",
                null,
                null,
                null);
            var progressFrame = new IpcStreamFrame(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcStreamFrameKinds.Progress,
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint),
                IpcPayloadCodec.SerializeToElement(progressPayload),
                Response: null);
            return InvokeProgressAndCreateResponseAsync(
                request,
                onProgressFrame,
                progressFrame,
                cancellationToken);
        });
        var client = new SupervisorClient(transportClient);
        var progressSink = new CollectingProgressSink();

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(progressSink.Entries);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithProgressSink_WhenKnownEventHasNoStreamPayloadContract_DropsProgressAndPreservesTerminal ()
    {
        var transportClient = new StreamingTransportClient((request, onProgressFrame, cancellationToken) =>
        {
            var progressFrame = new IpcStreamFrame(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcStreamFrameKinds.Progress,
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.Completed),
                IpcPayloadCodec.SerializeToElement(new { payloadKind = "startupObservation" }),
                Response: null);
            return InvokeProgressAndCreateResponseAsync(
                request,
                onProgressFrame,
                progressFrame,
                cancellationToken);
        });
        var client = new SupervisorClient(transportClient);
        var progressSink = new CollectingProgressSink();

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(progressSink.Entries);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithProgressSink_WhenProgressEnvelopeDoesNotMatchRequest_DropsProgressAndPreservesTerminal ()
    {
        var transportClient = new StreamingTransportClient((request, onProgressFrame, cancellationToken) =>
        {
            var progressPayload = new DaemonStartStartupObservationProgressEntry(
                ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation),
                "other-fingerprint",
                5000,
                "gui",
                "auto",
                null,
                "user",
                false,
                42,
                null,
                "waitingForEndpoint",
                null,
                "endpointRegistration",
                null,
                null,
                null);
            var progressFrame = new IpcStreamFrame(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcStreamFrameKinds.Progress,
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint),
                IpcPayloadCodec.SerializeToElement(progressPayload),
                Response: null);
            return InvokeProgressAndCreateResponseAsync(
                request,
                onProgressFrame,
                progressFrame,
                cancellationToken);
        });
        var client = new SupervisorClient(transportClient);
        var progressSink = new CollectingProgressSink();

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            progressSink,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(progressSink.Entries);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WithProgressSink_WhenProgressSinkThrows_PreservesTerminal ()
    {
        var transportClient = new StreamingTransportClient((request, onProgressFrame, cancellationToken) =>
        {
            var progressPayload = new DaemonStartStartupObservationProgressEntry(
                ContractLiteralCodec.ToValue(DaemonStartProgressPayloadKind.StartupObservation),
                "fingerprint",
                5000,
                "gui",
                "auto",
                null,
                "user",
                false,
                42,
                null,
                "waitingForEndpoint",
                null,
                "endpointRegistration",
                null,
                null,
                null);
            var progressFrame = new IpcStreamFrame(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcStreamFrameKinds.Progress,
                ContractLiteralCodec.ToValue(DaemonStartProgressEvent.WaitingForEndpoint),
                IpcPayloadCodec.SerializeToElement(progressPayload),
                Response: null);
            return InvokeProgressAndCreateResponseAsync(
                request,
                onProgressFrame,
                progressFrame,
                cancellationToken);
        });
        var client = new SupervisorClient(transportClient);

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            TimeSpan.FromSeconds(5),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            new ThrowingProgressSink(),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenSupervisorReturnsAttached_ReturnsAttachedResult ()
    {
        var session = CreateSession();
        var lifecycleSnapshot = new DaemonStartLifecycleSnapshot(
            IpcEditorLifecycleStateCodec.Ready,
            null,
            CanAcceptExecutionRequests: true);
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (endpoint, request, timeout, cancellationToken) => ValueTask.FromResult(new IpcResponse(
                ProtocolVersion: request.ProtocolVersion,
                RequestId: request.RequestId,
                Status: IpcProtocol.StatusOk,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningResponse(
                        StartStatus: "attached",
                        DaemonStatus: "running",
                        Session: session,
                        LifecycleSnapshot: lifecycleSnapshot)),
                Errors: [])),
        };
        var client = new SupervisorClient(transportClient);

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            TimeSpan.FromMilliseconds(100),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DaemonStartStatus.Attached, result.Status);
        Assert.Equal(session, result.Session);
        Assert.Equal(lifecycleSnapshot, result.LifecycleSnapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureRunning_WhenFailurePayloadContainsDiagnosisAndStartup_ReturnsFailureWithMetadata ()
    {
        var diagnosis = CreateDiagnosis();
        var startup = CreateStartupObservation();
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (endpoint, request, timeout, cancellationToken) => ValueTask.FromResult(new IpcResponse(
                ProtocolVersion: request.ProtocolVersion,
                RequestId: request.RequestId,
                Status: IpcProtocol.StatusError,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningFailureResponse("stale", diagnosis, startup)),
                Errors:
                [
                    new IpcError(ExecutionErrorCodes.IpcTimeout, "endpoint registration timed out", null),
                ])),
        };
        var client = new SupervisorClient(transportClient);

        var result = await client.EnsureRunningAsync(
            CreateManifest(),
            CreateUnityProject(),
            TimeSpan.FromMilliseconds(100),
            editorMode: DaemonEditorMode.Gui,
            onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        Assert.Equal(diagnosis, result.Diagnosis);
        Assert.Equal(startup, result.Startup);
        Assert.Equal(DaemonStatusKind.Stale, result.DaemonStatus);
    }

    private static SupervisorInstanceManifest CreateManifest ()
    {
        return new SupervisorInstanceManifest(
            ProcessId: Environment.ProcessId,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-supervisor-test",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));
    }

    private static ResolvedUnityProjectContext CreateUnityProject ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/repo/UnityProject",
            RepositoryRoot: "/repo",
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            EditorMode: "gui",
            OwnerKind: "cli",
            CanShutdownProcess: true,
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli.sock",
            ProcessId: 42,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 1, TimeSpan.Zero),
            OwnerProcessId: Environment.ProcessId);
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
            StartupStatus: DaemonStartupStatusValues.Blocked,
            StartupBlockingReason: DaemonStartupBlockingReasonValues.Compile,
            LaunchAttemptId: null,
            ProcessAction: DaemonStartupProcessActionValues.Kept,
            RetryDisposition: DaemonStartupRetryDispositionValues.RetryAfterFix);
    }

    private static async ValueTask<IpcResponse> InvokeProgressAndCreateResponseAsync (
        IpcRequest request,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        IpcStreamFrame progressFrame,
        CancellationToken cancellationToken)
    {
        await onProgressFrame(progressFrame, cancellationToken).ConfigureAwait(false);
        return new IpcResponse(
            ProtocolVersion: request.ProtocolVersion,
            RequestId: request.RequestId,
            Status: IpcProtocol.StatusOk,
            Payload: IpcPayloadCodec.SerializeToElement(
                new SupervisorIpcContracts.EnsureRunningResponse(
                    StartStatus: "started",
                    DaemonStatus: "running",
                    Session: CreateSession(),
                    LifecycleSnapshot: new DaemonStartLifecycleSnapshot(
                        IpcEditorLifecycleStateCodec.Ready,
                        null,
                        CanAcceptExecutionRequests: true))),
            Errors: []);
    }

    private sealed record StreamingTransportCall (
        IpcEndpoint Endpoint,
        IpcRequest Request,
        TimeSpan Timeout,
        bool UsesUnboundedResponseWait);

    private sealed class StreamingTransportClient : IIpcTransportClient
    {
        private readonly Func<IpcRequest, Func<IpcStreamFrame, CancellationToken, ValueTask>, CancellationToken, ValueTask<IpcResponse>> streamingHandler;

        public StreamingTransportClient (Func<IpcRequest, Func<IpcStreamFrame, CancellationToken, ValueTask>, CancellationToken, ValueTask<IpcResponse>> streamingHandler)
        {
            this.streamingHandler = streamingHandler;
        }

        public List<StreamingTransportCall> StreamingCalls { get; } = [];

        public ValueTask<IpcResponse> SendAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Expected streaming transport.");
        }

        public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan sendTimeout,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Expected streaming transport.");
        }

        public ValueTask<IpcResponse> SendStreamingAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan timeout,
            Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Expected unbounded streaming transport.");
        }

        public ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan sendTimeout,
            Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
            CancellationToken cancellationToken = default)
        {
            StreamingCalls.Add(new StreamingTransportCall(endpoint, request, sendTimeout, UsesUnboundedResponseWait: true));
            return streamingHandler(request, onProgressFrame, cancellationToken);
        }
    }

    private sealed class CollectingProgressSink : ICommandProgressSink
    {
        public List<(string EventName, object Payload)> Entries { get; } = [];

        public ValueTask OnEntryAsync<TPayload> (
            string eventName,
            TPayload payload,
            CancellationToken cancellationToken)
            where TPayload : notnull
        {
            cancellationToken.ThrowIfCancellationRequested();
            Entries.Add((eventName, payload));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingProgressSink : ICommandProgressSink
    {
        public ValueTask OnEntryAsync<TPayload> (
            string eventName,
            TPayload payload,
            CancellationToken cancellationToken)
            where TPayload : notnull
        {
            throw new IOException("Simulated progress sink failure.");
        }
    }
}
