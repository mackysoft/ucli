using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

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
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("fp-a"),
            State: DaemonListItemState.Stale,
            Reason: DaemonListItemReason.StaleSession,
            IssuedAtUtc: new DateTimeOffset(2026, 03, 09, 12, 0, 0, TimeSpan.Zero),
            ProcessId: 1234,
            ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 09, 11, 59, 0, TimeSpan.Zero),
            EditorMode: DaemonEditorMode.Batchmode,
            OwnerKind: DaemonSessionOwnerKind.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: IpcTransportKind.UnixDomainSocket,
            EndpointAddress: "/tmp/ucli.sock",
            LifecycleState: null,
            BlockingReason: null,
            CompileState: null,
            Generations: null,
            CanAcceptExecutionRequests: null,
            ObservedAtUtc: null,
            ActionRequired: null,
            PrimaryDiagnostic: null,
            Diagnosis: new DaemonDiagnosisOutput(
                Reason: "shutdownRequested",
                Message: "daemon shutdown completed",
                ReportedBy: DaemonDiagnosisReportedByValues.Unity,
                IsInferred: false,
                UpdatedAtUtc: new DateTimeOffset(2026, 03, 09, 12, 1, 0, TimeSpan.Zero),
                ProcessId: 1234,
                EditorInstancePath: null,
                ProcessStartedAtUtc: new DateTimeOffset(2026, 03, 09, 11, 59, 0, TimeSpan.Zero),
                UnityLogPath: "/repo/wt-a/.ucli/local/fingerprints/fp-a/unity.log",
                StartupPhase: DaemonDiagnosisStartupPhase.EndpointRegistration,
                ActionRequired: DaemonDiagnosisActionRequiredValues.InspectUnityLog,
                PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                    Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
                    Code: "CS0103",
                    File: "Assets/Foo.cs",
                    Line: 12,
                    Column: 34,
                    Message: "The name 'MissingType' does not exist in the current context")));
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
        var command = new DaemonListCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.ListAsync(
            projectPath: "/repo/wt-a/UnityProject",
            timeout: "3000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonList);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("items", 1)
                .HasProperty("items", 0, staleItem => staleItem
                    .HasString("state", "stale")
                    .HasString("reason", "staleSession")
                    .HasString("processStartedAtUtc", "2026-03-09T11:59:00+00:00")
                    .HasString("editorMode", "batchmode")
                    .HasString("ownerKind", "cli")
                    .HasBoolean("canShutdownProcess", true)
                    .HasProperty("diagnosis", diagnosis => diagnosis
                        .HasString("reason", "shutdownRequested")
                        .HasString("message", "daemon shutdown completed")
                        .HasString("reportedBy", DaemonDiagnosisReportedByValues.Unity)
                        .HasBoolean("isInferred", false)
                        .HasInt32("processId", 1234)
                        .HasString("processStartedAtUtc", "2026-03-09T11:59:00+00:00")
                        .HasString("unityLogPath", "/repo/wt-a/.ucli/local/fingerprints/fp-a/unity.log")
                        .HasString("startupPhase", ContractLiteralCodec.ToValue(DaemonDiagnosisStartupPhase.EndpointRegistration))
                        .HasString("actionRequired", DaemonDiagnosisActionRequiredValues.InspectUnityLog)
                        .HasProperty("primaryDiagnostic", primaryDiagnostic => primaryDiagnostic
                            .HasString("kind", DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler)
                            .HasString("code", "CS0103")
                            .HasString("file", "Assets/Foo.cs")
                            .HasInt32("line", 12)
                            .HasInt32("column", 34)
                            .HasString("message", "The name 'MissingType' does not exist in the current context")))));

        var itemJson = outputJson.RootElement
            .GetProperty("payload")
            .GetProperty("items")[0];
        Assert.False(itemJson.TryGetProperty("message", out _));
        Assert.False(itemJson.TryGetProperty("runtime", out _));
        Assert.False(itemJson.TryGetProperty("runtimeKind", out _));
    }
}
