using MackySoft.Tests;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Command;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonListCommandTests
{
    private static readonly SemaphoreSlim ConsoleOutputLock = new(1, 1);

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WritesDiagnosisAndOmitsLegacyMessageProperty ()
    {
        var item = new DaemonListItemOutput(
            WorktreePath: "/repo/wt-a",
            BranchRef: "refs/heads/main",
            Head: "aaaaaaaa",
            ProjectPath: "/repo/wt-a/UnityProject",
            ProjectFingerprint: "fp-a",
            State: DaemonListStateCodec.Stale,
            Reason: DaemonListReasonCodec.StaleSession,
            IssuedAtUtc: new DateTimeOffset(2026, 03, 09, 12, 0, 0, TimeSpan.Zero),
            ProcessId: 1234,
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli.sock",
            Diagnosis: new DaemonDiagnosisOutput(
                Reason: "shutdownRequested",
                Message: "daemon shutdown completed",
                ReportedBy: DaemonDiagnosisReportedByValues.Unity,
                IsInferred: false,
                UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 12, 1, 0, TimeSpan.Zero),
                ProcessId: 1234));
        var service = new StubDaemonListCommandService(
            DaemonListExecutionResult.Success(new DaemonListExecutionOutput(
                TimeoutMilliseconds: 3000,
                ProjectRelativePath: "UnityProject",
                IsComplete: true,
                CompletionReason: null,
                RemainingWorktreeCount: 0,
                Items:
                [
                    item,
                ])));
        var command = new DaemonListCommand(service);

        CommandExecutionState.Reset();
        var (exitCode, standardOutput) = await ExecuteAndCaptureStandardOutput(() => command.List(
            projectPath: "/repo/wt-a/UnityProject",
            timeout: "3000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonList,
            "ok",
            (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("items", 1)
                .HasProperty("items", 0, staleItem => staleItem
                    .HasString("state", "stale")
                    .HasString("reason", "staleSession")
                    .HasProperty("diagnosis", diagnosis => diagnosis
                        .HasString("reason", "shutdownRequested")
                        .HasString("message", "daemon shutdown completed")
                        .HasString("reportedBy", DaemonDiagnosisReportedByValues.Unity)
                        .HasBoolean("isInferred", false)
                        .HasInt32("processId", 1234))));

        var itemJson = outputJson.RootElement
            .GetProperty("payload")
            .GetProperty("items")[0];
        Assert.False(itemJson.TryGetProperty("message", out _));
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

    private sealed class StubDaemonListCommandService : IDaemonListCommandService
    {
        private readonly DaemonListExecutionResult result;

        public StubDaemonListCommandService (DaemonListExecutionResult result)
        {
            this.result = result;
        }

        public ValueTask<DaemonListExecutionResult> GetList (
            string? projectPath,
            string? timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }
}