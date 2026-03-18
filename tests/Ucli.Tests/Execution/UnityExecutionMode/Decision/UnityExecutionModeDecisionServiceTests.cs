using MackySoft.Tests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.ReadIndex;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Execution.Mode;

public sealed class UnityExecutionModeDecisionServiceTests
{
    private const string CommandName = "status";

    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithDaemonMode_WhenDaemonIsRunning_ReturnsDaemonTarget ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig();

        var result = await service.Decide(new UcliCommand(CommandName), "daemon", null, config, CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.HasContractError);
        Assert.Null(result.Error);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Daemon, decision.RequestedMode);
        Assert.True(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Daemon, decision.Target);
        Assert.Equal(TimeSpan.FromMilliseconds(IpcTimeoutDefaults.GlobalTimeoutMilliseconds), decision.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithDaemonMode_WhenDaemonIsNotRunning_ReturnsContractError ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.NotRunning());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig();

        var result = await service.Decide(new UcliCommand(CommandName), "daemon", null, config, CreateContext(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasContractError);
        Assert.Null(result.Decision);
        Assert.Null(result.Error);
        var contractError = Assert.IsType<UnityExecutionModeDecisionContractError>(result.ContractError);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, contractError.Code);
        Assert.Equal("Daemon is not running for mode=daemon.", contractError.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithAutoMode_WhenDaemonIsRunning_ReturnsDaemonTarget ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig();

        var result = await service.Decide(new UcliCommand(CommandName), "auto", null, config, CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Auto, decision.RequestedMode);
        Assert.True(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Daemon, decision.Target);
        Assert.Equal(TimeSpan.FromMilliseconds(IpcTimeoutDefaults.GlobalTimeoutMilliseconds), decision.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithAutoMode_WhenDaemonIsNotRunning_ReturnsOneshotTarget ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.NotRunning());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig();

        var result = await service.Decide(new UcliCommand(CommandName), "auto", null, config, CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Auto, decision.RequestedMode);
        Assert.False(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Oneshot, decision.Target);
        Assert.Equal(TimeSpan.FromMilliseconds(IpcTimeoutDefaults.GlobalTimeoutMilliseconds), decision.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithOneshotMode_WhenDaemonIsRunning_ReturnsContractError ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig();

        var result = await service.Decide(new UcliCommand(CommandName), "oneshot", null, config, CreateContext(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasContractError);
        Assert.Null(result.Decision);
        Assert.Null(result.Error);
        var contractError = Assert.IsType<UnityExecutionModeDecisionContractError>(result.ContractError);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden, contractError.Code);
        Assert.Equal("Daemon is running for mode=oneshot.", contractError.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithOneshotMode_WhenDaemonIsNotRunning_ReturnsOneshotTarget ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.NotRunning());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig();

        var result = await service.Decide(new UcliCommand(CommandName), "oneshot", null, config, CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var decision = Assert.IsType<UnityExecutionModeDecision>(result.Decision);
        Assert.Equal(UnityExecutionMode.Oneshot, decision.RequestedMode);
        Assert.False(decision.DaemonRunning);
        Assert.Equal(UnityExecutionTarget.Oneshot, decision.Target);
        Assert.Equal(TimeSpan.FromMilliseconds(IpcTimeoutDefaults.GlobalTimeoutMilliseconds), decision.Timeout);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task Decide_WithInvalidMode_ReturnsInvalidArgumentError (string mode)
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig();

        var result = await service.Decide(new UcliCommand(CommandName), mode, null, config, CreateContext(), CancellationToken.None);

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
    public async Task Decide_WhenProbeFails_ReturnsProbeError ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Failure(
            ExecutionError.InternalError("probe failed")));
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig();

        var result = await service.Decide(new UcliCommand(CommandName), "auto", null, config, CreateContext(), CancellationToken.None);

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
    public async Task Decide_WhenCanceled_ThrowsOperationCanceledException ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                service.Decide(new UcliCommand(CommandName), "auto", null, config, CreateContext(), cancellationTokenSource.Token).AsTask(),
                "Canceled unity execution mode decision",
                AsyncWaitTimeout);
        });
        Assert.False(probe.WasCalled);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithTimeoutOption_UsesOptionValue ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 3000);

        var result = await service.Decide(new UcliCommand(CommandName), "auto", "4500", config, CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(4500), probe.LastTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(4500), result.Decision!.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithoutTimeoutOption_UsesConfigDefault ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 3200);

        var result = await service.Decide(new UcliCommand(CommandName), "auto", null, config, CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(3200), probe.LastTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(3200), result.Decision!.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithoutTimeoutOption_UsesCommandSpecificConfigDefault ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig(
            ipcDefaultTimeoutMilliseconds: 3200,
            ipcTimeoutMillisecondsByCommand: new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                [CommandName] = 7300,
            });

        var result = await service.Decide(new UcliCommand(CommandName), "auto", null, config, CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(7300), probe.LastTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(7300), result.Decision!.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithoutTimeoutOption_UsesConfigDefault_WhenCommandSpecificValueIsNull ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig(
            ipcDefaultTimeoutMilliseconds: 3200,
            ipcTimeoutMillisecondsByCommand: new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                [CommandName] = null,
            });

        var result = await service.Decide(new UcliCommand(CommandName), "auto", null, config, CreateContext(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(3200), probe.LastTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(3200), result.Decision!.Timeout);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("0")]
    public async Task Decide_WithInvalidTimeoutOption_ReturnsInvalidArgumentError (string timeoutOption)
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig(ipcDefaultTimeoutMilliseconds: 3000);

        var result = await service.Decide(new UcliCommand(CommandName), "auto", timeoutOption, config, CreateContext(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasContractError);
        Assert.Null(result.Decision);
        Assert.Null(result.ContractError);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("timeout", error.Message, StringComparison.Ordinal);
        Assert.False(probe.WasCalled);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Decide_WithInvalidCommandName_ThrowsArgumentException ()
    {
        var probe = new StubDaemonReachabilityProbe(DaemonReachabilityProbeResult.Running());
        var service = new UnityExecutionModeDecisionService(probe);
        var config = CreateConfig();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                service.Decide(new UcliCommand(" "), "auto", null, config, CreateContext(), CancellationToken.None).AsTask(),
                "Invalid command unity execution mode decision",
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

    private static UcliConfig CreateConfig (
        int ipcDefaultTimeoutMilliseconds = IpcTimeoutDefaults.GlobalTimeoutMilliseconds,
        IReadOnlyDictionary<string, int?>? ipcTimeoutMillisecondsByCommand = null)
    {
        return new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist:
            [
                UcliContractConstants.Config.DefaultOperationAllowlistPattern,
            ])
        {
            IpcDefaultTimeoutMilliseconds = ipcDefaultTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand = ipcTimeoutMillisecondsByCommand
                ?? new Dictionary<string, int?>(StringComparer.Ordinal),
        };
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