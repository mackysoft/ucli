using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class UnityExecutionModeDecisionServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task DecideAsync_WithDaemonMode_WhenDaemonIsRunning_ReturnsDaemonTarget ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync("daemon", CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.HasContractError);
        Assert.Null(result.Error);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Daemon, decision.RequestedMode);
        Assert.True(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Daemon, decision.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DecideAsync_WithDaemonMode_WhenDaemonIsNotRunning_ReturnsContractError ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.NotRunning());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync("daemon", CreateContext(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasContractError);
        Assert.Null(result.Decision);
        Assert.Null(result.Error);
        var contractError = Assert.IsType<ModeDecisionContractError>(result.ContractError);
        Assert.Equal(ModeDecisionErrorCodes.DaemonNotRunning, contractError.Code);
        Assert.Equal("Daemon is not running for mode=daemon.", contractError.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DecideAsync_WithAutoMode_WhenDaemonIsRunning_ReturnsDaemonTarget ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync("auto", CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Auto, decision.RequestedMode);
        Assert.True(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Daemon, decision.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DecideAsync_WithAutoMode_WhenDaemonIsNotRunning_ReturnsOneshotTarget ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.NotRunning());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync("auto", CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Auto, decision.RequestedMode);
        Assert.False(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Oneshot, decision.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DecideAsync_WithOneshotMode_WhenDaemonIsRunning_ReturnsContractError ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync("oneshot", CreateContext(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasContractError);
        Assert.Null(result.Decision);
        Assert.Null(result.Error);
        var contractError = Assert.IsType<ModeDecisionContractError>(result.ContractError);
        Assert.Equal(ModeDecisionErrorCodes.DaemonRunningOneshotForbidden, contractError.Code);
        Assert.Equal("Daemon is running for mode=oneshot.", contractError.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DecideAsync_WithOneshotMode_WhenDaemonIsNotRunning_ReturnsOneshotTarget ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.NotRunning());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync("oneshot", CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Oneshot, decision.RequestedMode);
        Assert.False(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Oneshot, decision.Target);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DecideAsync_WithInvalidMode_ReturnsInvalidArgumentError ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync("invalid", CreateContext(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasContractError);
        Assert.Null(result.Decision);
        Assert.Null(result.ContractError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal("Mode must be auto, daemon, or oneshot.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DecideAsync_WhenProbeFails_ReturnsProbeError ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Failure(
            ExecutionError.InternalError("probe failed")));
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync("auto", CreateContext(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasContractError);
        Assert.Null(result.Decision);
        Assert.Null(result.ContractError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("probe failed", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task DecideAsync_WhenCanceled_ThrowsOperationCanceledException ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await service.DecideAsync("auto", CreateContext(), cancellationTokenSource.Token);
        });
        Assert.False(probe.WasCalled);
    }

    private static ResolvedUnityProjectContext CreateContext ()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: projectRoot,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption,
            ConfigPath: Path.Combine(projectRoot, ".ucli", "config.json"));
    }

    private sealed class StubDaemonReachabilityProbe : IDaemonReachabilityProbe
    {
        private readonly DaemonReachabilityProbeResult probeResult;

        public StubDaemonReachabilityProbe (DaemonReachabilityProbeResult probeResult)
        {
            this.probeResult = probeResult;
        }

        public bool WasCalled { get; private set; }

        public ValueTask<DaemonReachabilityProbeResult> ProbeAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return ValueTask.FromResult(probeResult);
        }
    }
}
