using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorRequestDispatcherTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestDispatcherRequestValidationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenSessionTokenIsMissingAndMethodIsUnknown_ReturnsSessionTokenRequired ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: string.Empty,
                method: "unknown",
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcSessionErrorCodes.SessionTokenRequired, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenSessionTokenIsInvalidAndMethodIsUnknown_ReturnsSessionTokenInvalid ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "invalid-token",
                method: "unknown",
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcSessionErrorCodes.SessionTokenInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenProtocolVersionIsUnsupportedAndMethodIsUnknown_ReturnsProtocolVersionMismatch ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion + 1,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: "unknown",
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcProtocolErrorCodes.ProtocolVersionMismatch, error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("unknown")]
    [InlineData("SUPERVISOR.PING")]
    [InlineData("supervisor.ping ")]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenMethodIsNotCanonical_ReturnsCorrelatedMethodNotSupported (string? method)
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();
        var requestId = Guid.NewGuid();

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: method!,
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        Assert.Equal(requestId, response.RequestId);
        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcProtocolErrorCodes.IpcMethodNotSupported, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenMethodIsMissing_ReturnsCorrelatedMethodNotSupported ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();
        var requestId = Guid.NewGuid();
        var rawRequest = JsonSerializer.SerializeToElement(
            new
            {
                ProtocolVersion = IpcProtocol.CurrentVersion,
                RequestId = requestId,
                SessionToken = runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                Payload = IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                ResponseMode = ContractLiteralCodec.ToValue(IpcResponseMode.Single),
            },
            IpcJsonSerializerOptions.Default);

        var response = await SendRawJsonRequestAsync(dispatcher, runtimeContext, rawRequest);

        Assert.Equal(requestId, response.RequestId);
        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcProtocolErrorCodes.IpcMethodNotSupported, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenStreamResponseModeTargetsUnknownMethod_ReturnsMethodNotSupported ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();

        var frames = await SendStreamingRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: "unknown",
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream)));

        var terminalFrame = Assert.Single(frames);
        var response = Assert.IsType<IpcResponse>(terminalFrame.Response);
        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcProtocolErrorCodes.IpcMethodNotSupported, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenResponseModeIsUnsupported_ReturnsInvalidArgumentWithoutStartOperation ()
    {
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
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
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                responseMode: "unsupported"));

        DaemonStartOperationAssert.EnsureRunningRequestRejectedBeforeStartOperation(
            response,
            startOperation,
            UcliCoreErrorCodes.InvalidArgument,
            "Unsupported IPC response mode: unsupported.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenResponseModeIsNull_ReturnsInvalidArgumentWithNullPlaceholder ()
    {
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
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
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                responseMode: null!));

        DaemonStartOperationAssert.EnsureRunningRequestRejectedBeforeStartOperation(
            response,
            startOperation,
            UcliCoreErrorCodes.InvalidArgument,
            "Unsupported IPC response mode: <null>.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenResponseModeIsMissing_ReturnsInvalidArgumentWithNullPlaceholder ()
    {
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);
        var payload = IpcPayloadCodec.SerializeToElement(
            new SupervisorIpcContracts.EnsureRunningRequest(
                UnityProjectRoot: unityProjectRoot,
                ProjectFingerprint: projectFingerprint,
                DeadlineUtc: CreateEnsureRunningDeadline(1000),
                AttemptTimeoutMilliseconds: 1000,
                EditorMode: null,
                OnStartupBlocked: "auto"));
        var rawRequest = JsonSerializer.SerializeToElement(
            new
            {
                ProtocolVersion = IpcProtocol.CurrentVersion,
                RequestId = Guid.NewGuid(),
                SessionToken = runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                Method = ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                Payload = payload,
            },
            IpcJsonSerializerOptions.Default);

        var response = await SendRawJsonRequestAsync(dispatcher, runtimeContext, rawRequest);

        DaemonStartOperationAssert.EnsureRunningRequestRejectedBeforeStartOperation(
            response,
            startOperation,
            UcliCoreErrorCodes.InvalidArgument,
            "Unsupported IPC response mode: <null>.");
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
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream)));

        var terminalFrame = Assert.Single(frames);
        Assert.Equal(IpcStreamFrameKinds.Terminal, terminalFrame.Kind);
        var response = Assert.IsType<IpcResponse>(terminalFrame.Response);
        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains(ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning), error.Message, StringComparison.Ordinal);
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
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: "bad\u0000path",
                        ProjectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
                        DeadlineUtc: CreateEnsureRunningDeadline(1000),
                        AttemptTimeoutMilliseconds: 1000,
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        Assert.Equal(IpcProtocol.StatusError, invalidResponse.Status);
        var invalidError = Assert.Single(invalidResponse.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, invalidError.Code);

        var pingResponse = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

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
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: ProjectFingerprintTestFactory.Create("mismatched-fingerprint"),
                        DeadlineUtc: CreateEnsureRunningDeadline(1000),
                        AttemptTimeoutMilliseconds: 1000,
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("Project fingerprint does not match", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEditorModeIsInvalid_ReturnsInvalidArgumentWithoutStartOperation ()
    {
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
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
                        EditorMode: "unsupported",
                        OnStartupBlocked: "auto")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        DaemonStartOperationAssert.EnsureRunningRequestRejectedBeforeStartOperation(
            response,
            startOperation,
            UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenOnStartupBlockedIsInvalid_ReturnsInvalidArgumentWithoutStartOperation ()
    {
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
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
                        EditorMode: null,
                        OnStartupBlocked: "unsupported")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        DaemonStartOperationAssert.EnsureRunningRequestRejectedBeforeStartOperation(
            response,
            startOperation,
            UcliCoreErrorCodes.InvalidArgument);
    }
}
