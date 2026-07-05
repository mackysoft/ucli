using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStatusServiceRunningBudgetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenProbeConsumesBudget_PropagatesRemainingTimeoutToPingInfoRead ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 700);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Running(DaemonSessionTestFactory.Create()))
        {
            OnGetStatus = () => timeProvider.Advance(TimeSpan.FromMilliseconds(200)),
        };
        var pingInfoClient = CreateSuccessfulPingInfoClient();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new StubDaemonReachabilityClassifier(static _ => false),
            new RecordingDaemonSessionDiagnosisResolver(),
            timeProvider);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: 700, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonStatusServiceInvocationAssert.DaemonPingTelemetryRead(
            pingInfoClient,
            context,
            expectedTimeout: TimeSpan.FromMilliseconds(500),
            expectedSessionToken: DaemonSessionTestFactory.Create().SessionToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenProbeConsumesEntireBudget_ReturnsTimeoutBeforePingInfoRead ()
    {
        var timeProvider = new ManualTimeProvider();
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 300);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(
            DaemonStatusResult.Running(DaemonSessionTestFactory.Create()))
        {
            OnGetStatus = () => timeProvider.Advance(TimeSpan.FromMilliseconds(300)),
        };
        var pingInfoClient = CreateSuccessfulPingInfoClient();
        var service = CreateService(
            resolver,
            daemonStatusOperation,
            pingInfoClient,
            new StubDaemonReachabilityClassifier(static _ => false),
            new RecordingDaemonSessionDiagnosisResolver(),
            timeProvider);

        var result = await service.GetStatusAsync(projectPath: null, timeoutMilliseconds: 300, cancellationToken: CancellationToken.None);

        DaemonStatusServiceInvocationAssert.TimeoutReturnedBeforePingTelemetryRead(result, pingInfoClient);
    }
}
