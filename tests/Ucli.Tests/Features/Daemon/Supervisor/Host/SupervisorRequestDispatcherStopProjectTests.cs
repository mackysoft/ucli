using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorRequestDispatcherTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestDispatcherStopProjectTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenAttemptTimeoutExceedsIntegerContract_ReturnsInvalidArgumentWithoutDispatch ()
    {
        var stopOperation = new RecordingDaemonStopOperation();
        var dispatcher = CreateDispatcher(stopOperation: stopOperation);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(
            runtimeContext.StorageRoot,
            unityProjectRoot);

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "stop-request-oversized-attempt-timeout",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.StopProjectMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new
                    {
                        UnityProjectRoot = unityProjectRoot,
                        ProjectFingerprint = projectFingerprint,
                        DeadlineUtc = DateTimeOffset.MaxValue,
                        AttemptTimeoutMilliseconds = (long)int.MaxValue + 1,
                    }),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, Assert.Single(response.Errors).Code);
        Assert.Empty(stopOperation.Invocations);
    }

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
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(
            runtimeContext.StorageRoot,
            unityProjectRoot);
        var deadlineUtc = timeProvider.GetUtcNow().AddSeconds(1);
        timeProvider.Advance(TimeSpan.FromMilliseconds(400));

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "stop-request-delayed-delivery",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.StopProjectMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.StopProjectRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        DeadlineUtc: deadlineUtc,
                        AttemptTimeoutMilliseconds: 800)),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusOk, response.Status);
        var invocation = Assert.Single(stopOperation.Invocations);
        Assert.Equal(TimeSpan.FromMilliseconds(600), invocation.Timeout);
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
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(
            runtimeContext.StorageRoot,
            unityProjectRoot);
        var deadlineUtc = timeProvider.GetUtcNow().AddSeconds(1);
        timeProvider.ShiftUtc(TimeSpan.FromDays(-1));

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "stop-request-clock-rollback",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.StopProjectMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.StopProjectRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        DeadlineUtc: deadlineUtc,
                        AttemptTimeoutMilliseconds: 700)),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusOk, response.Status);
        Assert.Equal(TimeSpan.FromMilliseconds(700), Assert.Single(stopOperation.Invocations).Timeout);
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
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(
            runtimeContext.StorageRoot,
            unityProjectRoot);
        var deadlineUtc = timeProvider.GetUtcNow().AddSeconds(1);
        timeProvider.ShiftUtc(TimeSpan.FromDays(1));

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "stop-request-clock-forward",
                SessionToken: runtimeContext.Manifest.SessionToken,
                Method: SupervisorIpcContracts.StopProjectMethod,
                Payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.StopProjectRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        DeadlineUtc: deadlineUtc,
                        AttemptTimeoutMilliseconds: 700)),
                responseMode: IpcResponseMode.Single));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, Assert.Single(response.Errors).Code);
        Assert.Empty(stopOperation.Invocations);
    }
}
