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
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: string.Empty,
                method: "unknown",
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue));

        Assert.Equal(IpcResponseStatus.Error, response.Status);
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
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: "invalid-token",
                method: "unknown",
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue));

        Assert.Equal(IpcResponseStatus.Error, response.Status);
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
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion + 1,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: "unknown",
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue));

        Assert.Equal(IpcResponseStatus.Error, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcProtocolErrorCodes.ProtocolVersionMismatch, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenStreamRequestProtocolVersionIsUnsupported_WritesCurrentVersionTerminalFrame ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();
        var requestId = Guid.NewGuid();

        var frames = await SendStreamingRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion + 1,
                requestId: requestId,
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue));

        var terminalFrame = Assert.Single(frames);
        Assert.Equal(IpcProtocol.CurrentVersion, terminalFrame.ProtocolVersion);
        Assert.Equal(requestId, terminalFrame.RequestId);
        var response = Assert.IsType<IpcResponse>(terminalFrame.Response);
        Assert.Equal(IpcProtocol.CurrentVersion, response.ProtocolVersion);
        Assert.Equal(requestId, response.RequestId);
        Assert.Equal(IpcProtocolErrorCodes.ProtocolVersionMismatch, Assert.Single(response.Errors).Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenPingDeadlineExpired_ReturnsTimeoutInsteadOfSuccess ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.UnixEpoch,
                requestDeadlineRemainingMilliseconds: 1000));

        Assert.Equal(IpcResponseStatus.Error, response.Status);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, Assert.Single(response.Errors).Code);
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
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: requestId,
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: method!,
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue));

        Assert.Equal(requestId, response.RequestId);
        Assert.Equal(IpcResponseStatus.Error, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcProtocolErrorCodes.IpcMethodNotSupported, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenMethodContainsNewlineAndLongLiteral_DoesNotReflectUntrustedLiteral ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();
        var untrustedMarker = "untrusted-method-marker";
        var method = untrustedMarker + "\n" + new string('m', 4096);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: method,
                payload: IpcPayloadCodec.SerializeToElement(new UcliEmptyArgs()),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue));

        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcProtocolErrorCodes.IpcMethodNotSupported, error.Code);
        Assert.DoesNotContain(untrustedMarker, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', error.Message);
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
                RequestDeadlineUtc = DateTimeOffset.MaxValue,
                RequestDeadlineRemainingMilliseconds = int.MaxValue,
            },
            IpcJsonSerializerOptions.Default);

        var response = await SendRawJsonRequestAsync(dispatcher, runtimeContext, rawRequest);

        Assert.Equal(requestId, response.RequestId);
        Assert.Equal(IpcResponseStatus.Error, response.Status);
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
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: "unknown",
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue));

        var terminalFrame = Assert.Single(frames);
        var response = Assert.IsType<IpcResponse>(terminalFrame.Response);
        Assert.Equal(IpcResponseStatus.Error, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(IpcProtocolErrorCodes.IpcMethodNotSupported, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenResponseModeIsUnsupported_ReturnsGenericInvalidArgumentWithoutStartOperation ()
    {
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: null,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: "unsupported",
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

        DaemonStartOperationAssert.EnsureRunningRequestRejectedBeforeStartOperation(
            response,
            startOperation,
            UcliCoreErrorCodes.InvalidArgument,
            "Unsupported supervisor IPC response mode.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenResponseModeContainsNewlineAndLongLiteral_DoesNotReflectUntrustedLiteral ()
    {
        var dispatcher = CreateDispatcher();
        var runtimeContext = CreateRuntimeContext();
        var untrustedMarker = "untrusted-response-mode-marker";
        var responseMode = untrustedMarker + "\n" + new string('r', 4096);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: responseMode,
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue));

        var error = Assert.Single(response.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.DoesNotContain(untrustedMarker, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenResponseModeIsNull_ReturnsGenericInvalidArgument ()
    {
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: null,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: null!,
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

        DaemonStartOperationAssert.EnsureRunningRequestRejectedBeforeStartOperation(
            response,
            startOperation,
            UcliCoreErrorCodes.InvalidArgument,
            "Unsupported supervisor IPC response mode.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenResponseModeIsMissing_ReturnsGenericInvalidArgument ()
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
                EditorMode: null,
                OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto));
        var rawRequest = JsonSerializer.SerializeToElement(
            new
            {
                ProtocolVersion = IpcProtocol.CurrentVersion,
                RequestId = Guid.NewGuid(),
                SessionToken = runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                Method = ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                Payload = payload,
                RequestDeadlineUtc = CreateEnsureRunningDeadline(1000),
                RequestDeadlineRemainingMilliseconds = 1000,
            },
            IpcJsonSerializerOptions.Default);

        var response = await SendRawJsonRequestAsync(dispatcher, runtimeContext, rawRequest);

        DaemonStartOperationAssert.EnsureRunningRequestRejectedBeforeStartOperation(
            response,
            startOperation,
            UcliCoreErrorCodes.InvalidArgument,
            "Unsupported supervisor IPC response mode.");
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
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue));

        var terminalFrame = Assert.Single(frames);
        Assert.Equal(IpcStreamFrameKind.Terminal, terminalFrame.Kind);
        var response = Assert.IsType<IpcResponse>(terminalFrame.Response);
        Assert.Equal(IpcResponseStatus.Error, response.Status);
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
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: "bad\u0000path",
                        ProjectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
                        EditorMode: null,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

        Assert.Equal(IpcResponseStatus.Error, invalidResponse.Status);
        var invalidError = Assert.Single(invalidResponse.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, invalidError.Code);

        var pingResponse = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.Ping),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.MaxValue,
                requestDeadlineRemainingMilliseconds: int.MaxValue));

        Assert.Equal(IpcResponseStatus.Ok, pingResponse.Status);
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
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: ProjectFingerprintTestFactory.Create("mismatched-fingerprint"),
                        EditorMode: null,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

        Assert.Equal(IpcResponseStatus.Error, response.Status);
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
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: JsonSerializer.SerializeToElement(
                    new
                    {
                        UnityProjectRoot = unityProjectRoot,
                        ProjectFingerprint = projectFingerprint,
                        EditorMode = "unsupported",
                        OnStartupBlocked = "auto",
                    },
                    IpcJsonSerializerOptions.Default),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

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
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: JsonSerializer.SerializeToElement(
                    new
                    {
                        UnityProjectRoot = unityProjectRoot,
                        ProjectFingerprint = projectFingerprint,
                        EditorMode = (string?)null,
                        OnStartupBlocked = "unsupported",
                    },
                    IpcJsonSerializerOptions.Default),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

        DaemonStartOperationAssert.EnsureRunningRequestRejectedBeforeStartOperation(
            response,
            startOperation,
            UcliCoreErrorCodes.InvalidArgument);
    }
}
