using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Daemon;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonListCommandTests
{
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
        var service = new StubDaemonListService(
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
        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.List(
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

    private sealed class StubDaemonListService : IDaemonListService
    {
        private readonly DaemonListExecutionResult result;

        public StubDaemonListService (DaemonListExecutionResult result)
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
