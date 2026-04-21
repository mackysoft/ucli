using MackySoft.Tests;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Daemon;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonCleanupCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WritesSkipReasonPayload ()
    {
        var service = new StubDaemonCleanupService(
            DaemonCleanupExecutionResult.Success(new DaemonCleanupExecutionOutput(
                CleanupStatus: "skipped",
                SkipReason: "unsafeInvalidSession",
                TimeoutMilliseconds: 3000)));
        var command = new DaemonCleanupCommand(service);

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Cleanup(
            projectPath: "/repo/UnityProject",
            timeout: "3000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonCleanup,
            "ok",
            (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("cleanupStatus", "skipped")
                .HasString("skipReason", "unsafeInvalidSession")
                .HasInt32("timeoutMilliseconds", 3000));
    }

    private sealed class StubDaemonCleanupService : IDaemonCleanupService
    {
        private readonly DaemonCleanupExecutionResult result;

        public StubDaemonCleanupService (DaemonCleanupExecutionResult result)
        {
            this.result = result;
        }

        public ValueTask<DaemonCleanupExecutionResult> Cleanup (
            string? projectPath,
            string? timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }
}