using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using static MackySoft.Ucli.Tests.Ipc.UnityIpcRequestExecutorTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcRequestExecutorModeDecisionTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenAutoModeDecisionReturnsContractError_ReturnsContractFailureWithoutCallingClients ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "contract-error");
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => CreateSuccessResponse(Guid.NewGuid()));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => CreateSuccessResponse(Guid.NewGuid()));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var executor = CreateExecutor(
            new StubModeDecisionService(
                UnityExecutionModeDecisionResult.ContractFailure(
                    new UnityExecutionModeDecisionContractError(
                        UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                        "Daemon is not running for mode=daemon."))),
            new RecordingDaemonPingInfoClient(),
            new RecordingUnityUcliPluginLocator(),
            CreateClients(
                daemonTransportClient,
                oneshotTransportClient,
                new UnexpectedDaemonSessionConnectionProvider("Contract error should not resolve a daemon session."),
                launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateOpsReadPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        UnityIpcExecutionPathAssert.NoUnityExecutionWasStarted(
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenModeDecisionThrows_ReturnsInternalErrorWithoutCallingClients ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "mode-decision-exception");
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var pluginLocator = new RecordingUnityUcliPluginLocator();
        var executor = CreateExecutor(
            new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Auto,
                    true,
                    UnityExecutionTarget.Daemon,
                    DefaultTimeout)))
            {
                OnDecide = static _ => throw new InvalidOperationException("mode decision failed"),
            },
            new RecordingDaemonPingInfoClient(),
            pluginLocator,
            CreateClients(
                daemonTransportClient,
                oneshotTransportClient,
                new UnexpectedDaemonSessionConnectionProvider("Mode decision failure should not resolve a daemon session."),
                launcher));

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            DefaultTimeout,
            UcliConfig.CreateDefault(),
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateOpsReadPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Contains("Failed to decide Unity execution mode.", result.Message, StringComparison.Ordinal);
        UnityIpcExecutionPathAssert.NoPluginVerificationOrUnityExecutionWasStarted(
            pluginLocator,
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenModeDecisionConsumesSharedBudget_ReturnsTimeoutBeforeDispatch ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-ipc-request-executor", "mode-decision-budget");
        var timeProvider = new ManualTimeProvider();
        var daemonTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Daemon transport must not be called."));
        var oneshotTransportClient = new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot transport must not be called."));
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle()));
        var modeDecisionService = new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
            new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                true,
                UnityExecutionTarget.Daemon,
                TimeSpan.FromMilliseconds(100))))
        {
            TimeProvider = timeProvider,
            OnDecide = static context =>
            {
                ((ManualTimeProvider)context.TimeProvider).Advance(TimeSpan.FromMilliseconds(120));
            },
        };
        var executor = CreateExecutor(
            modeDecisionService,
            new RecordingDaemonPingInfoClient(),
            new RecordingUnityUcliPluginLocator(),
            CreateClients(
                daemonTransportClient,
                oneshotTransportClient,
                new UnexpectedDaemonSessionConnectionProvider("Mode decision timeout should not resolve a daemon session."),
                launcher),
            timeProvider);

        var result = await executor.ExecuteAsync(
            UcliCommandIds.Ops,
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(100),
            UcliConfig.CreateDefault(),
            ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath),
            CreateOpsReadPayload());

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        UnityExecutionModeDecisionServiceAssert.DecisionAttemptedWithTimeout(
            modeDecisionService,
            TimeSpan.FromMilliseconds(100));
        UnityIpcExecutionPathAssert.NoUnityExecutionWasStarted(
            daemonTransportClient,
            oneshotTransportClient,
            launcher);
    }
}
