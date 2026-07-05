using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Ready;

using static MackySoft.Ucli.Application.Tests.Features.Assurance.Ready.ReadyServiceTestSupport;

public sealed class ReadyServiceFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnsupportedDaemonLifecycleState_ReturnsCommandFailure ()
    {
        var service = CreateService(
            modeDecisionService: CreateModeDecisionService(
                UnityExecutionMode.Daemon,
                daemonRunning: true,
                UnityExecutionTarget.Daemon),
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(CreateReadyPingResponse(
                lifecycleState: "futureState",
                canAcceptExecutionRequests: false)));

        var result = await service.ExecuteAsync(CreateExecutionInput(UnityExecutionMode.Daemon, failFast: true));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("unsupported state", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithModeContractError_ReturnsCommandFailure ()
    {
        var service = CreateService(
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.ContractFailure(
                new UnityExecutionModeDecisionContractError(
                    UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                    "Daemon is not running for mode=daemon."))));

        var result = await service.ExecuteAsync(CreateExecutionInput(UnityExecutionMode.Daemon));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithDaemonTimeoutBeforeLifecycleObservation_ReturnsCommandFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var service = CreateService(
            modeDecisionService: CreateModeDecisionService(
                UnityExecutionMode.Daemon,
                daemonRunning: true,
                UnityExecutionTarget.Daemon),
            daemonPingInfoClient: new RecordingDaemonPingInfoClient(new TimeoutException("daemon ping timed out"))
            {
                OnPingAndRead = () => timeProvider.Advance(TimeSpan.FromDays(1)),
            },
            timeProvider: timeProvider);

        var result = await service.ExecuteAsync(CreateExecutionInput(UnityExecutionMode.Daemon));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithOneshotStartupFailure_PreservesStartupFailureOnCommandFailure ()
    {
        var startupFailure = CreateStartupFailureDetail();
        var service = CreateService(
            modeDecisionService: CreateModeDecisionService(
                UnityExecutionMode.Auto,
                daemonRunning: false,
                UnityExecutionTarget.Oneshot),
            unityRequestExecutor: new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                DaemonErrorCodes.DaemonStartupBlocked,
                "Unity startup is blocked.",
                startupFailure))));

        var result = await service.ExecuteAsync(CreateExecutionInput(failFast: true));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Same(startupFailure, error.StartupFailure);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithOneshotPingProjectFingerprintMismatch_ReturnsCommandFailure ()
    {
        var service = CreateService(
            modeDecisionService: CreateModeDecisionService(
                UnityExecutionMode.Auto,
                daemonRunning: false,
                UnityExecutionTarget.Oneshot),
            unityRequestExecutor: new RecordingUnityRequestExecutor(CreateReadyPingSuccess(projectFingerprint: "other-fingerprint")));

        var result = await service.ExecuteAsync(CreateExecutionInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("projectFingerprint mismatch", error.Message, StringComparison.Ordinal);
    }
}
