using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonCleanupCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WritesSkipReasonPayload ()
    {
        var service = new StubDaemonCleanupService(
            DaemonCleanupExecutionResult.Success(new DaemonCleanupExecutionOutput(
                CleanupStatus: DaemonCleanupStatus.Skipped,
                SkipReason: DaemonCleanupSkipReason.UnsafeInvalidSession,
                DeletedLaunchAttemptCount: 0,
                TimeoutMilliseconds: 3000)));
        var command = new DaemonCleanupCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.CleanupAsync(
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
                .HasInt32("deletedLaunchAttemptCount", 0)
                .HasInt32("timeoutMilliseconds", 3000));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WhenCompleted_WritesDeletedLaunchAttemptCountPayload ()
    {
        var service = new StubDaemonCleanupService(
            DaemonCleanupExecutionResult.Success(new DaemonCleanupExecutionOutput(
                CleanupStatus: DaemonCleanupStatus.Completed,
                SkipReason: DaemonCleanupSkipReason.None,
                DeletedLaunchAttemptCount: 3,
                TimeoutMilliseconds: 3000)));
        var command = new DaemonCleanupCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await StandardOutputCapture.ExecuteAsync(() => command.CleanupAsync(
            projectPath: "/repo/UnityProject",
            timeout: "3000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("cleanupStatus", "completed")
                .IsNull("skipReason")
                .HasInt32("deletedLaunchAttemptCount", 3)
                .HasInt32("timeoutMilliseconds", 3000));
    }

    private sealed class StubDaemonCleanupService : IDaemonCleanupService
    {
        private readonly DaemonCleanupExecutionResult result;

        public StubDaemonCleanupService (DaemonCleanupExecutionResult result)
        {
            this.result = result;
        }

        public ValueTask<DaemonCleanupExecutionResult> CleanupAsync (
            string? projectPath,
            int? timeoutMilliseconds,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }
}
