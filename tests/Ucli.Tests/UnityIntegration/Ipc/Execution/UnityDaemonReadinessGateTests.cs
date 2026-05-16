using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonReadinessGateTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenWaitableStateBecomesReady_DispatchesFailFastRequest ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-daemon-readiness-gate", "wait-ready");
        var timeProvider = new ManualTimeProvider();
        var pingClient = new StubDaemonPingInfoClient(
            CreatePingPayload(IpcEditorLifecycleStateCodec.Busy, false),
            CreatePingPayload(IpcEditorLifecycleStateCodec.Ready, true));
        var daemonClient = new StubUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, timeProvider);
        var executionTask = gate.ExecuteAsync(
            CreateContext(scope),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            UnityIpcExecutionBudget.Start(TimeSpan.FromSeconds(30), timeProvider),
            daemonClient,
            CancellationToken.None).AsTask();

        while (pingClient.CallCount < 1)
        {
            await Task.Yield();
        }

        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result = await executionTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(2, pingClient.CallCount);
        Assert.Single(daemonClient.Requests);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            daemonClient.Requests[0].Payload,
            out IpcOpsReadRequest dispatchedPayload,
            out _));
        Assert.True(dispatchedPayload.FailFast);
        Assert.True(dispatchedPayload.RequireReadinessGate);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenFailFastBusyState_ReturnsEditorBusyWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-daemon-readiness-gate", "fail-fast-busy");
        var pingClient = new StubDaemonPingInfoClient(CreatePingPayload(IpcEditorLifecycleStateCodec.Busy, false));
        var daemonClient = new StubUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient);

        var result = await gate.ExecuteAsync(
            CreateContext(scope),
            CreateOpsReadDispatchRequest(failFast: true),
            new IpcOpsReadRequest(FailFast: true, RequireReadinessGate: true),
            UnityIpcExecutionBudget.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(EditorLifecycleErrorCodes.EditorBusy, result.ErrorCode);
        Assert.Empty(daemonClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDomainReloadingState_ReturnsEditorDomainReloadingWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-daemon-readiness-gate", "domain-reloading");
        var pingClient = new StubDaemonPingInfoClient(CreatePingPayload(IpcEditorLifecycleStateCodec.DomainReloading, false));
        var daemonClient = new StubUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient);

        var result = await gate.ExecuteAsync(
            CreateContext(scope),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            UnityIpcExecutionBudget.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(EditorLifecycleErrorCodes.EditorDomainReloading, result.ErrorCode);
        Assert.Equal(1, pingClient.CallCount);
        Assert.Empty(daemonClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenGuiSessionIsInPlaymode_ReturnsEditorPlaymodeWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-daemon-readiness-gate", "gui-playmode");
        var pingClient = new StubDaemonPingInfoClient(IpcPingResponseTestFactory.Create(
            editorMode: DaemonEditorModeValues.Gui,
            lifecycleState: IpcEditorLifecycleStateCodec.Playmode,
            canAcceptExecutionRequests: false));
        var daemonClient = new StubUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient);

        var result = await gate.ExecuteAsync(
            CreateContext(scope),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            UnityIpcExecutionBudget.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(EditorLifecycleErrorCodes.EditorPlaymode, result.ErrorCode);
        Assert.Equal(1, pingClient.CallCount);
        Assert.Empty(daemonClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadinessProbeTimesOutThenReady_RetriesAndDispatches ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-daemon-readiness-gate", "probe-timeout-ready");
        var timeProvider = new ManualTimeProvider();
        var pingClient = new StubDaemonPingInfoClient(
            new TimeoutException("probe timed out"),
            CreatePingPayload(IpcEditorLifecycleStateCodec.Ready, true));
        var daemonClient = new StubUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, timeProvider);
        var executionTask = gate.ExecuteAsync(
            CreateContext(scope),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            UnityIpcExecutionBudget.Start(TimeSpan.FromSeconds(30), timeProvider),
            daemonClient,
            CancellationToken.None).AsTask();

        while (pingClient.CallCount < 1)
        {
            await Task.Yield();
        }

        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var result = await executionTask;

        Assert.True(result.IsSuccess);
        Assert.Equal(2, pingClient.CallCount);
        Assert.Single(daemonClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadinessProbeReportsDaemonNotRunning_ReturnsFailureWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-daemon-readiness-gate", "probe-daemon-not-running");
        var pingClient = new StubDaemonPingInfoClient(new SocketException((int)SocketError.ConnectionRefused));
        var daemonClient = new StubUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient);

        var result = await gate.ExecuteAsync(
            CreateContext(scope),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            UnityIpcExecutionBudget.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        Assert.Equal(1, pingClient.CallCount);
        Assert.Empty(daemonClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadinessProbeThrowsUnexpectedException_ReturnsInternalErrorWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-daemon-readiness-gate", "probe-unexpected");
        var pingClient = new StubDaemonPingInfoClient(new InvalidOperationException("probe failed"));
        var daemonClient = new StubUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient);

        var result = await gate.ExecuteAsync(
            CreateContext(scope),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            UnityIpcExecutionBudget.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("probe failed", result.Message, StringComparison.Ordinal);
        Assert.Equal(1, pingClient.CallCount);
        Assert.Empty(daemonClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnsupportedLifecycleState_ReturnsInternalErrorWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-daemon-readiness-gate", "unsupported");
        var pingClient = new StubDaemonPingInfoClient(CreatePingPayload("unsupported", false));
        var daemonClient = new StubUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient);

        var result = await gate.ExecuteAsync(
            CreateContext(scope),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            UnityIpcExecutionBudget.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Empty(daemonClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenLateWaitableRegressionOccurs_RewaitsAndRedispatches ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-daemon-readiness-gate", "late-regression");
        var pingClient = new StubDaemonPingInfoClient(
            CreatePingPayload(IpcEditorLifecycleStateCodec.Ready, true),
            CreatePingPayload(IpcEditorLifecycleStateCodec.Ready, true));
        var daemonClient = new StubUnityIpcClient(
            UnityRequestExecutionResult.Success(UnityRequestResponseTestFactory.Create(CreateErrorResponse(
                EditorLifecycleErrorCodes.EditorBusy,
                "Unity editor is busy with internal work."))),
            CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient);

        var result = await gate.ExecuteAsync(
            CreateContext(scope),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            UnityIpcExecutionBudget.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            daemonClient,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, pingClient.CallCount);
        Assert.Equal(2, daemonClient.Requests.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenBudgetIsExhausted_ReturnsTimeoutWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-daemon-readiness-gate", "timeout");
        var timeProvider = new ManualTimeProvider();
        var budget = UnityIpcExecutionBudget.Start(TimeSpan.FromMilliseconds(100), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(120));
        var pingClient = new StubDaemonPingInfoClient(CreatePingPayload(IpcEditorLifecycleStateCodec.Ready, true));
        var daemonClient = new StubUnityIpcClient(CreateSuccessResult());
        var gate = new UnityDaemonReadinessGate(pingClient, timeProvider);

        var result = await gate.ExecuteAsync(
            CreateContext(scope),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            budget,
            daemonClient,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Equal(0, pingClient.CallCount);
        Assert.Empty(daemonClient.Requests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCancellationIsRequested_ThrowsOperationCanceledException ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-daemon-readiness-gate", "canceled");
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var gate = new UnityDaemonReadinessGate(new StubDaemonPingInfoClient());

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await gate.ExecuteAsync(
            CreateContext(scope),
            CreateOpsReadDispatchRequest(failFast: false),
            new IpcOpsReadRequest(FailFast: false, RequireReadinessGate: true),
            UnityIpcExecutionBudget.Start(TimeSpan.FromSeconds(30), TimeProvider.System),
            new StubUnityIpcClient(CreateSuccessResult()),
            cancellationTokenSource.Token).AsTask());
    }

    private static ResolvedUnityProjectContext CreateContext (TestDirectoryScope scope)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: scope.GetPath("UnityProject"),
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static UnityIpcDispatchRequest CreateOpsReadDispatchRequest (bool failFast)
    {
        return new UnityIpcDispatchRequest(
            IpcMethodNames.OpsRead,
            IpcPayloadCodec.SerializeToElement(new IpcOpsReadRequest(
                FailFast: failFast,
                RequireReadinessGate: true)));
    }

    private static UnityRequestExecutionResult CreateSuccessResult ()
    {
        return UnityRequestExecutionResult.Success(UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-success",
            Status: IpcProtocol.StatusOk,
            Payload: EmptyPayload(),
            Errors: [])));
    }

    private static IpcResponse CreateErrorResponse (
        UcliErrorCode code,
        string message)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-error",
            Status: IpcProtocol.StatusError,
            Payload: EmptyPayload(),
            Errors:
            [
                new IpcError(code, message, null),
            ]);
    }

    private static JsonElement EmptyPayload ()
    {
        return JsonDocument.Parse("{}").RootElement.Clone();
    }

    private static IpcPingResponse CreatePingPayload (
        string lifecycleState,
        bool canAcceptExecutionRequests)
    {
        return IpcPingResponseTestFactory.Create(
            lifecycleState: lifecycleState,
            canAcceptExecutionRequests: canAcceptExecutionRequests);
    }

    private sealed class StubDaemonPingInfoClient : IDaemonPingInfoClient
    {
        private readonly Queue<object> responses = new Queue<object>();

        public StubDaemonPingInfoClient (params object[] responses)
        {
            foreach (var response in responses)
            {
                this.responses.Enqueue(response);
            }
        }

        public int CallCount { get; private set; }

        public ValueTask<IpcPingResponse> PingAndReadAsync (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            string? sessionToken = null,
            bool validateProjectFingerprint = true,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            if (responses.Count == 0)
            {
                throw new Xunit.Sdk.XunitException("No daemon ping response was configured.");
            }

            var next = responses.Dequeue();
            if (next is Exception exception)
            {
                throw exception;
            }

            return ValueTask.FromResult((IpcPingResponse)next);
        }
    }

    private sealed class StubUnityIpcClient : IUnityIpcClient
    {
        private readonly Queue<UnityRequestExecutionResult> results = new Queue<UnityRequestExecutionResult>();

        public StubUnityIpcClient (params UnityRequestExecutionResult[] results)
        {
            foreach (var result in results)
            {
                this.results.Enqueue(result);
            }
        }

        public UnityExecutionTarget Target => UnityExecutionTarget.Daemon;

        public List<UnityIpcDispatchRequest> Requests { get; } = [];

        public ValueTask<UnityRequestExecutionResult> SendAsync (
            ResolvedUnityProjectContext unityProject,
            UnityIpcDispatchRequest dispatchRequest,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(dispatchRequest);
            if (results.Count == 0)
            {
                throw new Xunit.Sdk.XunitException("No daemon dispatch result was configured.");
            }

            return ValueTask.FromResult(results.Dequeue());
        }
    }
}
