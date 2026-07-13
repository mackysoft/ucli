using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using static MackySoft.Ucli.Tests.Ipc.UnityIpcRequestExecutorTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcRequestExecutorDaemonReadinessTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenDaemonOpsReadRequiresReadinessGate_ConvertsDispatchToFailFastGate ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "daemon-ops-readiness");
        var response = CreateSuccessResponse("req-daemon-readiness");
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => response);
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token"));
        var readinessProbe = new RecordingDaemonPingInfoClient(
            CreatePingPayload(IpcEditorLifecycleState.Busy, false),
            CreatePingPayload(IpcEditorLifecycleState.Ready, true));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout))),
            readinessProbe,
            new RecordingUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, sessionConnectionProvider, launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            unityProject,
            CreateOpsReadPayload(failFast: false, requireReadinessGate: true));

        Assert.True(result.IsSuccess);
        AssertSuccessfulUnityResponse(response, result.Response);
        DaemonPingInfoClientAssert.ReadinessProbeRetriedFor(readinessProbe, unityProject, CancellationToken.None);
        UnityIpcExecutionPathAssert.DaemonFailFastReadinessOpsReadDispatchedOnly(
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenDaemonOpsReadLateBusyRegressionOccurs_RewaitsAndRedispatches ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "daemon-ops-late-busy");
        var responses = new Queue<IpcResponse>(new[]
        {
            CreateErrorResponse(
                "req-daemon-busy",
                EditorLifecycleErrorCodes.EditorBusy,
                "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            CreateSuccessResponse("req-daemon-ready"),
        });
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => responses.Dequeue());
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(CreateConnectionResult("daemon-token"));
        var readinessProbe = new RecordingDaemonPingInfoClient(
            CreatePingPayload(IpcEditorLifecycleState.Ready, true),
            CreatePingPayload(IpcEditorLifecycleState.Ready, true));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout))),
            readinessProbe,
            new RecordingUnityUcliPluginLocator(),
            CreateClients(daemonTransportClient, oneshotTransportClient, sessionConnectionProvider, launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            unityProject,
            CreateOpsReadPayload(failFast: false, requireReadinessGate: true));

        Assert.True(result.IsSuccess);
        DaemonPingInfoClientAssert.ReadinessProbeRetriedFor(readinessProbe, unityProject, CancellationToken.None);
        UnityIpcExecutionPathAssert.DaemonFailFastReadinessOpsReadRedispatchedOnly(
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenDaemonOpsReadFailFastHitsBusyState_ReturnsLifecycleFailureWithoutDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "daemon-ops-fail-fast");
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var readinessProbe = new RecordingDaemonPingInfoClient(
            CreatePingPayload(IpcEditorLifecycleState.Busy, false));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout))),
            readinessProbe,
            new RecordingUnityUcliPluginLocator(),
            CreateClients(
                daemonTransportClient,
                oneshotTransportClient,
                new UnexpectedDaemonSessionConnectionProvider("Fail-fast busy state should not resolve a daemon session."),
                launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            unityProject,
            CreateOpsReadPayload(failFast: true, requireReadinessGate: true));

        Assert.False(result.IsSuccess);
        Assert.Equal(EditorLifecycleErrorCodes.EditorBusy, result.ErrorCode);
        Assert.Contains("Unity editor is busy with internal work.", result.Message, StringComparison.Ordinal);
        DaemonPingInfoClientAssert.ReadinessProbeAttemptedOnceFor(readinessProbe, unityProject, CancellationToken.None);
        UnityIpcExecutionPathAssert.NoUnityExecutionWasStarted(
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }
}
