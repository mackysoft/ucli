using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
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

        var response = await SendRequest(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-missing-token",
                SessionToken: string.Empty,
                Method: SupervisorIpcContracts.PingMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion))));

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

        var response = await SendRequest(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-invalid-token",
                SessionToken: "invalid-token",
                Method: SupervisorIpcContracts.PingMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion))));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcSessionErrorCodes.SessionTokenInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenUnityProjectRootIsInvalid_ReturnsInvalidArgumentWithoutBreakingSubsequentRequests ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();

        var invalidResponse = await SendRequest(
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
                        EditorMode: null))));

        Assert.Equal(IpcProtocol.StatusError, invalidResponse.Status);
        var invalidError = Assert.Single(invalidResponse.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, invalidError.Code);

        var pingResponse = await SendRequest(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "request-2",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.PingMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion))));

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

        var response = await SendRequest(
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
                        EditorMode: null))));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("Project fingerprint does not match", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEditorModeIsSpecified_PassesNormalizedValueToStartOperation ()
    {
        var startOperation = new StubDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequest(
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
                        EditorMode: " gui "))));

        Assert.True(
            string.Equals(IpcProtocol.StatusOk, response.Status, StringComparison.Ordinal),
            string.Join(Environment.NewLine, response.Errors.Select(error => $"{error.Code.Value}: {error.Message}")));
        Assert.Equal(DaemonEditorMode.Gui, startOperation.LastEditorMode);
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

        var response = await SendRequest(
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
                        EditorMode: "unsupported"))));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Equal(0, startOperation.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEnsureRunningFailsWithDiagnosis_EmitsDiagnosisPayload ()
    {
        var diagnosis = CreateDiagnosis();
        var startOperation = new StubDaemonStartOperation
        {
            StartResult = DaemonStartResult.Failure(
                ExecutionError.Timeout("endpoint registration timed out", ExecutionErrorCodes.IpcTimeout),
                diagnosis),
        };
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequest(
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
                        EditorMode: null))));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningFailureResponse payload,
            out _));
        Assert.Equal(diagnosis, payload.Diagnosis);
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

    private static async Task<IpcResponse> SendRequest (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        IpcRequest request)
    {
        using var stream = new NonDisconnectingMemoryStream();
        await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
        var requestLength = stream.Length;
        stream.Position = 0;

        await dispatcher.HandleConnection(stream, runtimeContext, CancellationToken.None).ConfigureAwait(false);

        stream.Position = requestLength;
        return await IpcFrameCodec.ReadModelAsync<IpcResponse>(
                stream,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
    }

    private sealed class NonDisconnectingMemoryStream : MemoryStream
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

    private sealed class StubDaemonStartOperation : IDaemonStartOperation
    {
        public DaemonStartResult StartResult { get; set; } = DaemonStartResult.AlreadyRunning(CreateSession());

        public int CallCount { get; private set; }

        public DaemonEditorMode? LastEditorMode { get; private set; }

        public ValueTask<DaemonStartResult> Start (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            DaemonEditorMode? editorMode,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastEditorMode = editorMode;
            return ValueTask.FromResult(StartResult);
        }
    }

    private sealed class StubDaemonStopOperation : IDaemonStopOperation
    {
        public ValueTask<DaemonStopResult> Stop (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonStopResult.Stopped());
        }
    }

    private sealed class StubDaemonPingClient : IDaemonPingClient
    {
        public ValueTask Ping (
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
        public ValueTask<DaemonDiagnosisReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisReadResult.Success(null));
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Write (
            string storageRoot,
            string projectFingerprint,
            DaemonDiagnosis diagnosis,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }

        public ValueTask<DaemonDiagnosisStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonDiagnosisStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonSessionStore : IDaemonSessionStore
    {
        public ValueTask<DaemonSessionReadResult> Read (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionReadResult.Success(null));
        }

        public ValueTask<DaemonSessionStoreOperationResult> Write (
            string storageRoot,
            DaemonSession session,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }

        public ValueTask<DaemonSessionStoreOperationResult> Delete (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }

    private sealed class StubDaemonArtifactCleaner : IDaemonArtifactCleaner
    {
        public ValueTask<DaemonSessionStoreOperationResult> Cleanup (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(DaemonSessionStoreOperationResult.Success());
        }
    }

    private static DaemonSession CreateSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli.sock",
            ProcessId: 42,
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
}
