using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Hosting.Cli.Daemon.Logs;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsUnityClearCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Clear_WhenServiceSucceeds_WritesJsonEnvelope ()
    {
        var command = new LogsUnityClearCommand(
            new StubLogsUnityClearService(LogsUnityClearServiceResult.Success(new LogsUnityClearServiceOutput("cleared", 4500))),
            CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ClearAsync(
            projectPath: "/tmp/unity-project",
            timeout: "4500"));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.LogsUnityClear,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement.GetProperty("payload"))
            .HasString("clearStatus", "cleared")
            .HasInt32("timeoutMilliseconds", 4500);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Clear_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubLogsUnityClearService(LogsUnityClearServiceResult.Success(new LogsUnityClearServiceOutput("cleared", 3000)));
        var command = new LogsUnityClearCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.ClearAsync(timeout: "0"));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Equal(0, service.CallCount);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.LogsUnityClear,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, "INVALID_ARGUMENT");
    }

    private sealed class StubLogsUnityClearService : ILogsUnityClearService
    {
        private readonly LogsUnityClearServiceResult result;

        public StubLogsUnityClearService (LogsUnityClearServiceResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public ValueTask<LogsUnityClearServiceResult> ExecuteAsync (
            LogsUnityClearServiceRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }
}
