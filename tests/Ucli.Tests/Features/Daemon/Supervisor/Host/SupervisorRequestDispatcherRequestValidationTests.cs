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
        var startOperation = new RecordingDaemonStartOperation();
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

        DaemonStartOperationAssert.EnsureRunningRequestRejectedBeforeStartOperation(
            response,
            startOperation,
            UcliCoreErrorCodes.InvalidArgument);
    }
}
