using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonStopCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenServiceReportsNotRunning_WritesNotRunningPayload ()
    {
        var service = new StubDaemonStopService(
            DaemonStopExecutionResult.Success(new DaemonStopExecutionOutput(
                StopStatus: DaemonStopStatus.NotRunning,
                DaemonStatus: DaemonStatusKind.NotRunning,
                TimeoutMilliseconds: UcliContractConstants.Config.IpcTimeoutDefaultDaemonStopMilliseconds,
                Session: null)));
        var command = new DaemonStopCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.StopAsync(
            projectPath: "/repo/UnityProject",
            timeout: null,
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonStop);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("stopStatus", "notRunning")
                .HasString("daemonStatus", "notRunning")
                .HasInt32("timeoutMilliseconds", UcliContractConstants.Config.IpcTimeoutDefaultDaemonStopMilliseconds)
                .IsNull("session"));
    }

}
