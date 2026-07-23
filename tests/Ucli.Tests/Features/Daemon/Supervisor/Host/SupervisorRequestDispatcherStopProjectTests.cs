using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorRequestDispatcherTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestDispatcherStopProjectTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenRequestDeliveryIsDelayed_UsesDeadlineRemainingAtReceipt ()
    {
        var timeProvider = new ManualTimeProvider();
        var stopOperation = new RecordingDaemonStopOperation();
        var dispatcher = CreateDispatcher(
            timeProvider: timeProvider,
            stopOperation: stopOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = AbsolutePath.Parse(Path.Combine(runtimeContext.StorageRoot.Value, "UnityProject"));
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(
            runtimeContext.StorageRoot,
            unityProjectRoot);
        var deadlineUtc = timeProvider.GetUtcNow().AddSeconds(1);
        timeProvider.Advance(TimeSpan.FromMilliseconds(400));

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.StopProject),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.StopProjectRequest(
                        UnityProjectRoot: unityProjectRoot.Value,
                        ProjectFingerprint: projectFingerprint)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: deadlineUtc,
                requestDeadlineRemainingMilliseconds: 800));

        Assert.Equal(IpcResponseStatus.Ok, response.Status);
        var invocation = Assert.Single(stopOperation.Invocations);
        Assert.Equal(TimeSpan.FromMilliseconds(600), invocation.RemainingTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenUtcClockMovesBackward_CapsExecutionWithAttemptTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var stopOperation = new RecordingDaemonStopOperation();
        var dispatcher = CreateDispatcher(
            timeProvider: timeProvider,
            stopOperation: stopOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = AbsolutePath.Parse(Path.Combine(runtimeContext.StorageRoot.Value, "UnityProject"));
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(
            runtimeContext.StorageRoot,
            unityProjectRoot);
        var deadlineUtc = timeProvider.GetUtcNow().AddSeconds(1);
        timeProvider.ShiftUtc(TimeSpan.FromDays(-1));

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.StopProject),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.StopProjectRequest(
                        UnityProjectRoot: unityProjectRoot.Value,
                        ProjectFingerprint: projectFingerprint)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: deadlineUtc,
                requestDeadlineRemainingMilliseconds: 700));

        Assert.Equal(IpcResponseStatus.Ok, response.Status);
        Assert.Equal(TimeSpan.FromMilliseconds(700), Assert.Single(stopOperation.Invocations).RemainingTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenUtcClockMovesPastDeadline_ReturnsStructuredTimeoutWithoutDispatch ()
    {
        var timeProvider = new ManualTimeProvider();
        var stopOperation = new RecordingDaemonStopOperation();
        var dispatcher = CreateDispatcher(
            timeProvider: timeProvider,
            stopOperation: stopOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = AbsolutePath.Parse(Path.Combine(runtimeContext.StorageRoot.Value, "UnityProject"));
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(
            runtimeContext.StorageRoot,
            unityProjectRoot);
        var deadlineUtc = timeProvider.GetUtcNow().AddSeconds(1);
        timeProvider.ShiftUtc(TimeSpan.FromDays(1));

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.StopProject),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.StopProjectRequest(
                        UnityProjectRoot: unityProjectRoot.Value,
                        ProjectFingerprint: projectFingerprint)),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: deadlineUtc,
                requestDeadlineRemainingMilliseconds: 700));

        Assert.Equal(IpcResponseStatus.Error, response.Status);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, Assert.Single(response.Errors).Code);
        Assert.Empty(stopOperation.Invocations);
    }
}
