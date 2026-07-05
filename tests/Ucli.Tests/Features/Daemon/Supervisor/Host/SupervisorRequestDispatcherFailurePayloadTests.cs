using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorRequestDispatcherTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestDispatcherFailurePayloadTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEnsureRunningFailsWithDiagnosisAndStartup_EmitsFailureMetadataPayload ()
    {
        var diagnosis = DaemonDiagnosisTestFactory.CreateGuiEndpointNotRegistered();
        var startup = CreateStartupObservation();
        var startOperation = new RecordingDaemonStartOperation
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
        var diagnosis = DaemonDiagnosisTestFactory.CreateGuiEndpointNotRegistered();
        var startOperation = new RecordingDaemonStartOperation
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
    public async Task HandleConnection_WhenCallerDisconnectsDuringEnsureRunning_ReturnsIpcTimeout ()
    {
        var startOperation = new RecordingDaemonStartOperation
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
}
