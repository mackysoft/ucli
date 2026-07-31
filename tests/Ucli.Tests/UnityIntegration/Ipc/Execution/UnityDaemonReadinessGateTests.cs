using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonReadinessGateTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenWaitableStateBecomesReady_DispatchesFailFastRequest ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingClient = new RecordingDaemonPingInfoClient(
            CreatePingPayload(UnityEditorLifecycleState.Busy),
            CreatePingPayload(UnityEditorLifecycleState.Ready));
        var daemonClient = new RecordingUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, timeProvider);
        var unityProject = CreateContext("wait-ready");
        var executionTask = gate.ExecuteAsync(
            unityProject,
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            daemonClient,
            CancellationToken.None).AsTask();

        await pingClient.WaitForFirstInvocationAsync("Daemon readiness initial probe", AsyncWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result = await executionTask;

        Assert.True(result.IsSuccess);
        DaemonPingInfoClientAssert.ReadinessProbeRetriedFor(pingClient, unityProject, CancellationToken.None);
        UnityIpcClientAssert.FailFastOpsReadDispatchedOnce(daemonClient, unityProject);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenFailFastBusyState_ReturnsEditorBusyWithoutDispatch ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingPayload(
            UnityEditorLifecycleState.Busy));
        var daemonClient = new RecordingUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, TimeProvider.System);

        var result = await gate.ExecuteAsync(
            CreateContext("fail-fast-busy"),
            CreateOpsReadDispatchRequest(failFast: true),
            new IpcOpsReadRequest(FailFast: true, RequireReadinessGate: true),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        UnityDaemonReadinessGateAssert.RejectedWithoutDispatch(
            result,
            daemonClient,
            EditorLifecycleErrorCodes.EditorBusy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDomainReloadingState_ReturnsEditorDomainReloadingWithoutDispatch ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingPayload(
            UnityEditorLifecycleState.DomainReloading));
        var daemonClient = new RecordingUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, TimeProvider.System);
        var unityProject = CreateContext("domain-reloading");

        var result = await gate.ExecuteAsync(
            unityProject,
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        DaemonPingInfoClientAssert.ReadinessProbeAttemptedOnceFor(pingClient, unityProject, CancellationToken.None);
        UnityDaemonReadinessGateAssert.RejectedWithoutDispatch(
            result,
            daemonClient,
            EditorLifecycleErrorCodes.EditorDomainReloading);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenGuiSessionIsInPlaymode_ReturnsEditorPlaymodeWithoutDispatch ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(UnityEditorObservationTestFactory.Create(
            UnityEditorLifecycleState.PlayMode,
            UnityEditorMode.Gui));
        var daemonClient = new RecordingUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, TimeProvider.System);
        var unityProject = CreateContext("gui-playmode");

        var result = await gate.ExecuteAsync(
            unityProject,
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        DaemonPingInfoClientAssert.ReadinessProbeAttemptedOnceFor(pingClient, unityProject, CancellationToken.None);
        UnityDaemonReadinessGateAssert.RejectedWithoutDispatch(
            result,
            daemonClient,
            EditorLifecycleErrorCodes.EditorPlaymode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadinessProbeTimesOutThenReady_RetriesAndDispatches ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingClient = new RecordingDaemonPingInfoClient(
            new TimeoutException("probe timed out"),
            CreatePingPayload(UnityEditorLifecycleState.Ready));
        var daemonClient = new RecordingUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, timeProvider);
        var unityProject = CreateContext("probe-timeout-ready");
        var executionTask = gate.ExecuteAsync(
            unityProject,
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            daemonClient,
            CancellationToken.None).AsTask();

        await pingClient.WaitForFirstInvocationAsync("Daemon readiness timeout probe", AsyncWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result = await executionTask;

        Assert.True(result.IsSuccess);
        DaemonPingInfoClientAssert.ReadinessProbeRetriedFor(pingClient, unityProject, CancellationToken.None);
        UnityIpcClientAssert.FailFastOpsReadDispatchedOnce(daemonClient, unityProject);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadinessProbeReportsDaemonNotRunning_ReturnsFailureWithoutDispatch ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            IpcConnectExceptionTestFactory.FromSocketError(SocketError.ConnectionRefused));
        var daemonClient = new RecordingUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, TimeProvider.System);
        var unityProject = CreateContext("probe-daemon-not-running");

        var result = await gate.ExecuteAsync(
            unityProject,
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        DaemonPingInfoClientAssert.ReadinessProbeAttemptedOnceFor(pingClient, unityProject, CancellationToken.None);
        UnityDaemonReadinessGateAssert.RejectedWithoutDispatch(
            result,
            daemonClient,
            UnityExecutionModeDecisionErrorCodes.DaemonNotRunning);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadinessProbeThrowsUnexpectedException_ReturnsInternalErrorWithoutDispatch ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(new InvalidOperationException("probe failed"));
        var daemonClient = new RecordingUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, TimeProvider.System);
        var unityProject = CreateContext("probe-unexpected");

        var result = await gate.ExecuteAsync(
            unityProject,
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        DaemonPingInfoClientAssert.ReadinessProbeAttemptedOnceFor(pingClient, unityProject, CancellationToken.None);
        UnityDaemonReadinessGateAssert.RejectedWithoutDispatch(
            result,
            daemonClient,
            UcliCoreErrorCodes.InternalError,
            "probe failed");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenLateWaitableRegressionOccurs_RewaitsAndRedispatches ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(
            CreatePingPayload(UnityEditorLifecycleState.Ready),
            CreatePingPayload(UnityEditorLifecycleState.Ready));
        var daemonClient = new RecordingUnityIpcClient(
            UnityRequestExecutionResult.Success(UnityRequestResponseTestFactory.Create(CreateErrorResponse(
                EditorLifecycleErrorCodes.EditorBusy,
                "Unity editor is busy with internal work."))),
            CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, TimeProvider.System);
        var unityProject = CreateContext("late-regression");

        var result = await gate.ExecuteAsync(
            unityProject,
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonPingInfoClientAssert.ReadinessProbeRetriedFor(pingClient, unityProject, CancellationToken.None);
        UnityIpcClientAssert.FailFastOpsReadRedispatched(daemonClient, unityProject);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenBudgetIsExhausted_ReturnsTimeoutWithoutDispatch ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(100), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(120));
        var pingClient = new RecordingDaemonPingInfoClient(CreatePingPayload(
            UnityEditorLifecycleState.Ready));
        var daemonClient = new RecordingUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, timeProvider);

        var result = await gate.ExecuteAsync(
            CreateContext("timeout"),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
                deadline,
            daemonClient,
            CancellationToken.None);

        UnityDaemonReadinessGateAssert.TimedOutBeforeProbeAndDispatch(
            result,
            pingClient,
            daemonClient);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCancellationIsRequested_ThrowsOperationCanceledException ()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var gate = new UnityDaemonReadinessGate(
            new RecordingDaemonPingInfoClient(),
            TimeProvider.System);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await gate.ExecuteAsync(
            CreateContext("canceled"),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            new RecordingUnityIpcClient(CreateSuccessResult()),
            cancellationTokenSource.Token).AsTask());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteLifecycleStartAdmission_WhenPolicyWaitsAndRetriesRejectedStart_ReusesDispatch ()
    {
        var timeProvider = new ManualTimeProvider();
        var pingClient = new RecordingDaemonPingInfoClient(
            CreatePingPayload(UnityEditorLifecycleState.Ready),
            CreatePingPayload(UnityEditorLifecycleState.Ready),
            CreatePingPayload(UnityEditorLifecycleState.Ready));
        var daemonClient = new RecordingUnityIpcClient(
            UnityRequestExecutionResult.Success(
                UnityRequestResponseTestFactory.Create(
                    CreateErrorResponse(
                        EditorLifecycleErrorCodes.EditorBusy,
                        "The Start request was rejected before persistence."))),
            CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, timeProvider);
        var policy = new RecordingStartAdmissionPolicy(
            UnityReadinessDecision.Wait(),
            UnityReadinessDecision.Ready(),
            UnityReadinessDecision.Ready());
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Compile,
                timeProvider: timeProvider,
                executionTimeout: TimeSpan.FromSeconds(30));
        var dispatchRequest = UnityIpcDispatchRequest.LifecycleExecution(
            UnityIpcMethod.Compile,
            registration,
            requiredStart: null,
            static start => IpcPayloadCodec.SerializeToElement(
                new IpcCompileRequest(start)),
            policy);

        var executionTask = gate.ExecuteLifecycleStartAdmissionAsync(
            CreateContext("lifecycle-start-policy"),
            dispatchRequest,
            policy,
            ExecutionDeadline.Start(TimeSpan.FromSeconds(30), timeProvider),
            daemonClient,
            CancellationToken.None).AsTask();

        await pingClient.WaitForFirstInvocationAsync(
            "Lifecycle start admission initial probe",
            AsyncWaitTimeout);
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result = await executionTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(3, policy.EvaluationCount);
        Assert.Equal(
            EditorLifecycleErrorCodes.EditorBusy,
            Assert.Single(policy.RejectedStartErrorCodes));
        Assert.Equal(2, daemonClient.Invocations.Count);
        Assert.All(
            daemonClient.Invocations,
            invocation => Assert.Same(
                dispatchRequest,
                invocation.DispatchRequest));
    }

    private static ResolvedUnityProjectContext CreateContext (string testCaseName)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine("unity-daemon-readiness-gate", testCaseName));
        return ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(repositoryRoot);
    }

    private static UnityIpcDispatchRequest CreateOpsReadDispatchRequest (bool failFast)
    {
        return new UnityIpcDispatchRequest(
            UnityIpcMethod.OpsRead,
            IpcPayloadCodec.SerializeToElement(new IpcOpsReadRequest(
                FailFast: failFast,
                RequireReadinessGate: true)),
            UnityBatchmodeLaunchOptions.Default);
    }

    private static UnityRequestExecutionResult CreateSuccessResult ()
    {
        return UnityRequestExecutionResult.Success(UnityRequestResponseTestFactory.Create(new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Ok,
            payload: EmptyPayload(),
            errors: [])));
    }

    private static IpcResponse CreateErrorResponse (
        UcliCode code,
        string message)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: IpcResponseStatus.Error,
            payload: EmptyPayload(),
            errors:
            [
                new IpcError(code, message, null),
            ]);
    }

    private static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private static UnityEditorObservation CreatePingPayload (UnityEditorLifecycleState lifecycleState)
    {
        return UnityEditorObservationTestFactory.Create(lifecycleState);
    }

    private sealed class RecordingStartAdmissionPolicy :
        ILifecycleExecutionStartAdmissionPolicy
    {
        private readonly Queue<UnityReadinessDecision> decisions;

        public RecordingStartAdmissionPolicy (
            params UnityReadinessDecision[] decisions)
        {
            this.decisions = new Queue<UnityReadinessDecision>(decisions);
        }

        public bool FailFast => false;

        public int EvaluationCount { get; private set; }

        public List<UcliCode> RejectedStartErrorCodes { get; } = [];

        public UnityReadinessDecision Evaluate (
            UnityEditorObservation observation)
        {
            ArgumentNullException.ThrowIfNull(observation);
            EvaluationCount++;
            return decisions.Dequeue();
        }

        public bool ShouldRetryAfterRejectedStart (UcliCode errorCode)
        {
            RejectedStartErrorCodes.Add(errorCode);
            return true;
        }
    }
}
