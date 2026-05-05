using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Shared.Execution.Process;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class UnityExecutionModeDecisionServiceTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(3000);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithDaemonMode_WhenDaemonIsRunning_ReturnsDaemonTarget ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.Decide(UnityExecutionMode.Daemon, CreateContext(), DefaultTimeout, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.HasContractError);
        Assert.Null(result.Error);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Daemon, decision.RequestedMode);
        Assert.True(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Daemon, decision.Target);
        Assert.Equal(DefaultTimeout, decision.Timeout);
        Assert.Equal(DefaultTimeout, probe.LastTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithAutoMode_WhenDaemonIsNotRunning_ReturnsOneshotTarget ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.NotRunning());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.Decide(UnityExecutionMode.Auto, CreateContext(), DefaultTimeout, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Auto, decision.RequestedMode);
        Assert.False(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Oneshot, decision.Target);
        Assert.Equal(DefaultTimeout, decision.Timeout);
        Assert.Equal(DefaultTimeout, probe.LastTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithOneshotMode_WhenDaemonIsRunning_ReturnsContractError ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.Decide(UnityExecutionMode.Oneshot, CreateContext(), DefaultTimeout, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasContractError);
        Assert.Null(result.Decision);
        Assert.Null(result.Error);
        var contractError = Assert.IsType<UnityExecutionModeDecisionContractError>(result.ContractError);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden, contractError.Code);
        Assert.Equal("Daemon is running for mode=oneshot.", contractError.Message);
        Assert.Equal(DefaultTimeout, probe.LastTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WhenProbeFails_ReturnsProbeError ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Failure(
            ExecutionError.InternalError("probe failed")));
        var service = new UnityExecutionModeDecisionService(probe);

        var result = await service.Decide(UnityExecutionMode.Auto, CreateContext(), DefaultTimeout, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasContractError);
        Assert.Null(result.Decision);
        Assert.Null(result.ContractError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("probe failed", error.Message);
        Assert.Equal(DefaultTimeout, probe.LastTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WhenCanceled_ThrowsOperationCanceledException ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                service.Decide(UnityExecutionMode.Auto, CreateContext(), DefaultTimeout, cancellationTokenSource.Token).AsTask(),
                "Canceled unity execution mode decision",
                AsyncWaitTimeout);
        });
        Assert.False(probe.WasCalled);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Decide_WithNonPositiveTimeout_ThrowsArgumentOutOfRangeException (int timeoutMilliseconds)
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                service.Decide(UnityExecutionMode.Auto, CreateContext(), TimeSpan.FromMilliseconds(timeoutMilliseconds), CancellationToken.None).AsTask(),
                "Invalid timeout unity execution mode decision",
                AsyncWaitTimeout);
        });
        Assert.False(probe.WasCalled);
    }

    private static ResolvedUnityProjectContext CreateContext ()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Unity"));
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: projectRoot,
            RepositoryRoot: projectRoot,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private sealed class StubDaemonReachabilityProbe : IDaemonReachabilityProbe
    {
        private readonly DaemonReachabilityProbeResult probeResult;

        public StubDaemonReachabilityProbe (DaemonReachabilityProbeResult probeResult)
        {
            this.probeResult = probeResult;
        }

        public bool WasCalled { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public ValueTask<DaemonReachabilityProbeResult> Probe (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastTimeout = timeout;
            return ValueTask.FromResult(probeResult);
        }
    }
}
