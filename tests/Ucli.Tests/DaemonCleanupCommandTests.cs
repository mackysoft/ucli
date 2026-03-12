using MackySoft.Tests;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Daemon.Command;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonCleanupCommandTests
{
    private static readonly SemaphoreSlim ConsoleOutputLock = new(1, 1);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Cleanup_WritesSkipReasonPayload ()
    {
        var service = new StubDaemonCleanupCommandService(
            DaemonCleanupExecutionResult.Success(new DaemonCleanupExecutionOutput(
                CleanupStatus: "skipped",
                SkipReason: "unsafeInvalidSession",
                TimeoutMilliseconds: 3000)));
        var command = new DaemonCleanupCommand(service);

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await ExecuteAndCaptureStandardOutput(() => command.Cleanup(
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

    private static async Task<(int ExitCode, string StandardOutput)> ExecuteAndCaptureStandardOutput (Func<Task<int>> action)
    {
        await ConsoleOutputLock.WaitAsync();
        var originalOutput = Console.Out;

        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            var exitCode = await action();
            await writer.FlushAsync();
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
            ConsoleOutputLock.Release();
        }
    }

    private sealed class StubDaemonCleanupCommandService : IDaemonCleanupCommandService
    {
        private readonly DaemonCleanupExecutionResult result;

        public StubDaemonCleanupCommandService (DaemonCleanupExecutionResult result)
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