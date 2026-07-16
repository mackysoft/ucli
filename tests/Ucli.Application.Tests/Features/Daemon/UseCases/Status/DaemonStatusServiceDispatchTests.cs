using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using static MackySoft.Ucli.Application.Tests.Daemon.DaemonStatusServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStatusServiceDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetStatus_WhenArgumentsAreSpecified_PropagatesToResolverAndOperation ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 2300);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var daemonStatusOperation = new RecordingDaemonStatusOperation(DaemonStatusResult.NotRunning(
            diagnosis: null,
            lastLaunchAttempt: null));
        var service = CreateService(
            resolver,
            daemonStatusOperation);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await service.GetStatusAsync(
            projectPath: "/tmp/sandbox-unity",
            timeoutMilliseconds: 9999,
            cancellationToken: cancellationToken);

        Assert.True(result.IsSuccess);
        DaemonStatusServiceInvocationAssert.StatusCommandResolvedAndOperationExecuted(
            resolver,
            daemonStatusOperation,
            context,
            expectedProjectPath: "/tmp/sandbox-unity",
            expectedTimeoutMilliseconds: 9999,
            cancellationToken);
    }
}
