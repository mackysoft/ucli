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
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

        Assert.Equal(IpcResponseStatus.Error, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningFailureResponse payload,
            out _));
        Assert.Equal(diagnosis, payload.Diagnosis);
        Assert.Equal(startup, payload.Startup);
        Assert.Equal(DaemonStatusKind.Stale, payload.DaemonStatus);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEnsureRunningFailureCompletesAfterRequestDeadline_EmitsDiagnosisPayload ()
    {
        var diagnosis = DaemonDiagnosisTestFactory.CreateGuiEndpointNotRegistered();
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero));
        var startOperation = new RecordingDaemonStartOperation
        {
            OnStart = (_, _) =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(2));
                return ValueTask.CompletedTask;
            },
            StartResult = DaemonStartResult.Failure(
                ExecutionError.Timeout("endpoint registration timed out", ExecutionErrorCodes.IpcTimeout),
                diagnosis),
        };
        var dispatcher = CreateDispatcher(startOperation, timeProvider);
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
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1),
                requestDeadlineRemainingMilliseconds: 1000));

        Assert.Equal(IpcResponseStatus.Error, response.Status);
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
    public async Task HandleConnection_WhenStreamingEnsureRunningFailureCompletesAfterRequestDeadline_EmitsDiagnosisPayload ()
    {
        var diagnosis = DaemonDiagnosisTestFactory.CreateGuiEndpointNotRegistered();
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero));
        var startOperation = new RecordingDaemonStartOperation
        {
            OnStart = (_, _) =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(2));
                return ValueTask.CompletedTask;
            },
            StartResult = DaemonStartResult.Failure(
                ExecutionError.Timeout("endpoint registration timed out", ExecutionErrorCodes.IpcTimeout),
                diagnosis),
        };
        var dispatcher = CreateDispatcher(startOperation, timeProvider);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
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
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        EditorMode: null,
                        OnStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Stream),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1),
                requestDeadlineRemainingMilliseconds: 1000));

        var terminalFrame = Assert.Single(frames);
        Assert.Equal(IpcStreamFrameKind.Terminal, terminalFrame.Kind);
        var response = Assert.IsType<IpcResponse>(terminalFrame.Response);
        Assert.Equal(IpcResponseStatus.Error, response.Status);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, Assert.Single(response.Errors).Code);
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
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: CreateEnsureRunningDeadline(1000),
                requestDeadlineRemainingMilliseconds: 1000));

        Assert.Equal(IpcResponseStatus.Error, response.Status);
        var error = Assert.Single(response.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Contains("caller disconnected", error.Message, StringComparison.Ordinal);
    }
}
