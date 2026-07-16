using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Hosting.Cli.Daemon.Logs;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsUnityClearCommandTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ClearHelp_ExplainsVisibleConsoleAndRetainedLogBoundary ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Logs,
            UcliCommandNames.UnitySubcommand,
            UcliCommandNames.ClearSubcommand,
            "--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("visible Unity Editor Console", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("logs unity read", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Clear_WhenServiceSucceeds_WritesJsonEnvelope ()
    {
        var command = new LogsUnityClearCommand(
            new RecordingLogsUnityClearService(LogsUnityClearServiceResult.Success(new LogsUnityClearServiceOutput(4500))),
            CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ClearAsync(
            projectPath: "/tmp/unity-project",
            timeout: "4500"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.LogsUnityClear);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        Assert.Equal(
            "Unity Editor Console display cleared; retained logs remain available to logs unity read.",
            outputJson.RootElement.GetProperty("message").GetString());
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasString("clearStatus", "cleared")
            .HasInt32("timeoutMilliseconds", 4500);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Clear_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new RecordingLogsUnityClearService(LogsUnityClearServiceResult.Success(new LogsUnityClearServiceOutput(3000)));
        var command = new LogsUnityClearCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ClearAsync(timeout: "0"));

        LogsCommandAssert.UnityClearInvalidArgumentReturnedWithoutExecution(
            exitCode,
            standardOutput,
            service);
    }

}
