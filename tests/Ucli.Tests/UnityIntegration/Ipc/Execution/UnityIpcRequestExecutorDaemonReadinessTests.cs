using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
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
        var response = CreateSuccessResponse(Guid.NewGuid());
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => response);
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var sessionStore = new QueuedDaemonSessionStore(CreateSessionReadResult("daemon-token"));
        var readinessProbe = new RecordingDaemonPingInfoClient(
            CreatePingPayload(UnityEditorLifecycleState.Busy),
            CreatePingPayload(UnityEditorLifecycleState.Ready));
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
            CreateClients(daemonTransportClient, oneshotTransportClient, sessionStore, launcher));

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
                Guid.NewGuid(),
                EditorLifecycleErrorCodes.EditorBusy,
                "Unity editor is busy with internal work. Retry without --failFast or wait until lifecycleState=ready before executing request."),
            CreateSuccessResponse(Guid.NewGuid()),
        });
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => responses.Dequeue());
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var sessionStore = new QueuedDaemonSessionStore(CreateSessionReadResult("daemon-token"));
        var readinessProbe = new RecordingDaemonPingInfoClient(
            CreatePingPayload(UnityEditorLifecycleState.Ready),
            CreatePingPayload(UnityEditorLifecycleState.Ready));
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
            CreateClients(daemonTransportClient, oneshotTransportClient, sessionStore, launcher));

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
            CreatePingPayload(UnityEditorLifecycleState.Busy));
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
                new UnexpectedDaemonSessionStore("Fail-fast busy state should not resolve a daemon session."),
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

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenRefreshStartWaitsUntilReady_DispatchesOneStartAndOneAction ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-ipc-request-executor",
            "daemon-refresh-wait");
        var response = CreateSuccessResponse(Guid.NewGuid());
        var daemonTransportClient =
            new RecordingUnityIpcTransportClient(_ => response);
        var oneshotTransportClient =
            new RecordingUnityIpcTransportClient(
                _ => throw new Xunit.Sdk.XunitException(
                    "Oneshot transport must not be called."));
        var readinessProbe = new RecordingDaemonPingInfoClient(
            CreatePingPayload(UnityEditorLifecycleState.Busy),
            CreatePingPayload(UnityEditorLifecycleState.Ready));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(
                new StubUnityBatchmodeProcessHandle()));
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var executor = CreateExecutor(
            new StubModeDecisionService(
                UnityExecutionModeDecisionResult.Success(
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
                new QueuedDaemonSessionStore(
                    CreateSessionReadResult("daemon-token")),
                launcher));
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Refresh);

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Refresh,
            UnityExecutionMode.Auto,
            DefaultTimeout
            + LifecycleExecutionTiming.ResponseDeliveryGrace,
            UcliConfig.CreateDefault(),
            unityProject,
            new UnityRequestPayload.Refresh(
                registration,
                requiredStart: null,
                new RefreshLifecycleExecutionStartAdmissionPolicy(
                    failFast: false)));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            registration.ExecutionId,
            result.LifecycleExecutionStart!.LifecycleExecutionRef.Id);
        Assert.True(result.LifecycleActionDispatched);
        Assert.Equal(2, readinessProbe.Invocations.Count);
        IpcRequestAssert.Methods(
            daemonTransportClient.Requests,
            UnityIpcMethod.LifecycleStart,
            UnityIpcMethod.Refresh);
        Assert.Empty(oneshotTransportClient.Requests);
        Assert.Empty(launcher.Invocations);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenNewLifecycleExecutionDeadlineHasExpired_DoesNotResolveTargetOrDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-ipc-request-executor",
            "daemon-lifecycle-start-deadline");
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var daemonTransportClient =
            new RecordingUnityIpcTransportClient(
                _ => throw new Xunit.Sdk.XunitException(
                    "Daemon transport must not be called."));
        var oneshotTransportClient =
            new RecordingUnityIpcTransportClient(
                _ => throw new Xunit.Sdk.XunitException(
                    "Oneshot transport must not be called."));
        var readinessProbe = new RecordingDaemonPingInfoClient();
        var launcher = new RecordingUnityBatchmodeProcessLauncher(
            UnityBatchmodeProcessLaunchResult.Success(
                new StubUnityBatchmodeProcessHandle()));
        var unityProject =
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(
                scope.FullPath);
        var executor = CreateExecutor(
            new StubModeDecisionService(
                UnityExecutionModeDecisionResult.Success(
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
                new UnexpectedDaemonSessionStore(
                    "Expired Lifecycle Execution must not resolve a daemon session."),
                launcher),
            timeProvider);
        var registration =
            UnityIpcRequestBuilderTestSupport.CreateLifecycleRegistration(
                LifecycleExecutionKind.Compile,
                timeProvider: timeProvider,
                executionTimeout: TimeSpan.FromMilliseconds(100));
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Compile,
            UnityExecutionMode.Auto,
            LifecycleExecutionTiming.ResponseDeliveryGrace
            + TimeSpan.FromMilliseconds(100),
            UcliConfig.CreateDefault(),
            unityProject,
            new UnityRequestPayload.Compile(
                registration,
                requiredStart: null));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Empty(readinessProbe.Invocations);
        UnityIpcExecutionPathAssert.NoUnityExecutionWasStarted(
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }
}
