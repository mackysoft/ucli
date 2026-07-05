using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Execution.Mode;

public sealed class UnityExecutionModeDecisionServiceTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(3000);

    private static readonly ResolvedUnityProjectContext UnityProject = ProjectContextTestFactory.CreateUnknownVersionUnityProject(
        projectFingerprint: "fingerprint");

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithDaemonMode_WhenDaemonIsRunning_ReturnsDaemonTarget ()
    {
        var probe = new RecordingDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync(UnityExecutionMode.Daemon, UnityProject, DefaultTimeout, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.HasContractError);
        Assert.Null(result.Error);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Daemon, decision.RequestedMode);
        Assert.True(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Daemon, decision.Target);
        Assert.Equal(DefaultTimeout, decision.Timeout);
        DaemonReachabilityProbeAssert.ProbeAttemptedFor(probe, UnityProject, DefaultTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithAutoMode_WhenDaemonIsNotRunning_ReturnsOneshotTarget ()
    {
        var probe = new RecordingDaemonReachabilityProbe(DaemonReachabilityProbeResult.NotRunning());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync(UnityExecutionMode.Auto, UnityProject, DefaultTimeout, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Auto, decision.RequestedMode);
        Assert.False(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Oneshot, decision.Target);
        Assert.Equal(DefaultTimeout, decision.Timeout);
        DaemonReachabilityProbeAssert.ProbeAttemptedFor(probe, UnityProject, DefaultTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithOneshotMode_WhenDaemonIsRunning_ReturnsContractError ()
    {
        var probe = new RecordingDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync(UnityExecutionMode.Oneshot, UnityProject, DefaultTimeout, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasContractError);
        Assert.Null(result.Decision);
        Assert.Null(result.Error);
        var contractError = Assert.IsType<UnityExecutionModeDecisionContractError>(result.ContractError);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden, contractError.Code);
        Assert.Equal("Daemon is running for mode=oneshot.", contractError.Message);
        DaemonReachabilityProbeAssert.ProbeAttemptedFor(probe, UnityProject, DefaultTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WhenProbeFails_ReturnsProbeError ()
    {
        var probe = new RecordingDaemonReachabilityProbe(DaemonReachabilityProbeResult.Failure(
            ExecutionError.InternalError("probe failed")));
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.DecideAsync(UnityExecutionMode.Auto, UnityProject, DefaultTimeout, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasContractError);
        Assert.Null(result.Decision);
        Assert.Null(result.ContractError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("probe failed", error.Message);
        DaemonReachabilityProbeAssert.ProbeAttemptedFor(probe, UnityProject, DefaultTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WhenCanceledBeforeProbe_ThrowsCancellationWithoutReachabilityProbe ()
    {
        var probe = new RecordingDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await DaemonReachabilityProbeAssert.CancellationRejectedBeforeProbeAsync(
            service.DecideAsync(UnityExecutionMode.Auto, UnityProject, DefaultTimeout, cancellationTokenSource.Token).AsTask(),
            probe,
            AsyncWaitTimeout);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Decide_WithNonPositiveTimeout_RejectsInputWithoutReachabilityProbe (int timeoutMilliseconds)
    {
        var probe = new RecordingDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);

        await DaemonReachabilityProbeAssert.InvalidTimeoutRejectedBeforeProbeAsync(
            service.DecideAsync(UnityExecutionMode.Auto, UnityProject, TimeSpan.FromMilliseconds(timeoutMilliseconds), CancellationToken.None).AsTask(),
            probe,
            AsyncWaitTimeout);
    }
}
