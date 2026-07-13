using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Supervisor.SupervisorRequestDispatcherTestSupport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorRequestDispatcherEnsureRunningTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenRequestDeliveryIsDelayed_UsesDeadlineRemainingAtReceipt ()
    {
        var timeProvider = new ManualTimeProvider();
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation, timeProvider);
        var runtimeContext = CreateRuntimeContext();
        var unityProjectRoot = Path.Combine(runtimeContext.StorageRoot, "UnityProject");
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(runtimeContext.StorageRoot, unityProjectRoot);
        var deadlineUtc = timeProvider.GetUtcNow().AddSeconds(1);
        timeProvider.Advance(TimeSpan.FromMilliseconds(400));

        var response = await SendRequestAsync(
            dispatcher,
            runtimeContext,
            new IpcRequest(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(new
                {
                    UnityProjectRoot = unityProjectRoot,
                    ProjectFingerprint = projectFingerprint,
                    DeadlineUtc = deadlineUtc,
                    AttemptTimeoutMilliseconds = 800,
                    EditorMode = (string?)null,
                    OnStartupBlocked = "auto",
                }),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        Assert.Equal(IpcProtocol.StatusOk, response.Status);
        var invocation = Assert.Single(startOperation.Invocations);
        Assert.Equal(TimeSpan.FromMilliseconds(600), invocation.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenUtcClockMovesBackward_CapsExecutionWithAttemptTimeout ()
    {
        var timeProvider = new ManualTimeProvider();
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation, timeProvider);
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
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        DeadlineUtc: deadlineUtc,
                        AttemptTimeoutMilliseconds: 700,
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        Assert.Equal(IpcProtocol.StatusOk, response.Status);
        Assert.Equal(TimeSpan.FromMilliseconds(700), Assert.Single(startOperation.Invocations).Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenUtcClockMovesPastDeadline_ReturnsStructuredTimeoutWithoutDispatch ()
    {
        var timeProvider = new ManualTimeProvider();
        var startOperation = new RecordingDaemonStartOperation();
        var dispatcher = CreateDispatcher(startOperation, timeProvider);
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
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                sessionToken: runtimeContext.Manifest.SessionToken.GetEncodedValue(),
                method: ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning),
                payload: IpcPayloadCodec.SerializeToElement(
                    new SupervisorIpcContracts.EnsureRunningRequest(
                        UnityProjectRoot: unityProjectRoot,
                        ProjectFingerprint: projectFingerprint,
                        DeadlineUtc: deadlineUtc,
                        AttemptTimeoutMilliseconds: 700,
                        EditorMode: null,
                        OnStartupBlocked: "auto")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        Assert.Equal(IpcProtocol.StatusError, response.Status);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, Assert.Single(response.Errors).Code);
        Assert.Empty(startOperation.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task HandleConnection_WhenEditorModeIsSpecified_PassesNormalizedValueToStartOperation ()
    {
        var lifecycleSnapshot = new DaemonStartLifecycleSnapshot(
            IpcEditorLifecycleStateCodec.Compiling,
            IpcEditorBlockingReasonCodec.Compile,
            CanAcceptExecutionRequests: false);
        var startOperation = new RecordingDaemonStartOperation
        {
            StartResult = DaemonStartResult.AlreadyRunning(
                DaemonSessionTestFactory.Create(
                    sessionToken: "session-token",
                    issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
                    endpointTransportKind: "unixDomainSocket",
                    endpointAddress: "/tmp/ucli.sock",
                    processId: 42,
                    ownerProcessId: 24),
                lifecycleSnapshot),
        };
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
                        EditorMode: " gui ",
                        OnStartupBlocked: " terminate ")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        Assert.True(
            string.Equals(IpcProtocol.StatusOk, response.Status, StringComparison.Ordinal),
            string.Join(Environment.NewLine, response.Errors.Select(error => $"{error.Code.Value}: {error.Message}")));
        DaemonStartOperationAssert.EnsureRunningRequested(
            startOperation,
            runtimeContext.StorageRoot,
            unityProjectRoot,
            projectFingerprint,
            TimeSpan.FromMilliseconds(1000),
            DaemonEditorMode.Gui,
            DaemonStartupBlockedProcessPolicy.Terminate);
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
        var session = DaemonSessionTestFactory.Create(
            sessionToken: "session-token",
            issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero),
            editorMode: "gui",
            ownerKind: "user",
            canShutdownProcess: false,
            endpointTransportKind: "unixDomainSocket",
            endpointAddress: "/tmp/ucli.sock",
            processId: 42,
            ownerProcessId: 24,
            editorInstanceId: DaemonSessionTestFactory.DefaultEditorInstanceId);
        var lifecycleSnapshot = new DaemonStartLifecycleSnapshot(
            IpcEditorLifecycleStateCodec.Ready,
            null,
            CanAcceptExecutionRequests: true);
        var startOperation = new RecordingDaemonStartOperation
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
                        EditorMode: "gui",
                        OnStartupBlocked: "auto")),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single)));

        Assert.Equal(IpcProtocol.StatusOk, response.Status);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningResponse payload,
            out _));
        Assert.Equal("attached", payload.StartStatus);
        Assert.Equal(DaemonSessionContractMapper.ToContract(session), payload.Session);
        Assert.Equal(lifecycleSnapshot, payload.LifecycleSnapshot);
    }
}
