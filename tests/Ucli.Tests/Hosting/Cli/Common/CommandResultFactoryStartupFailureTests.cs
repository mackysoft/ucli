using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Hosting.Cli.Requests;

namespace MackySoft.Ucli.Tests;

public sealed class CommandResultFactoryStartupFailureTests
{
    private static readonly CommandResultJsonContractWriter ResultWriter = new();

    private static StartupFailureCommandResultCase[] StartupFailureCommandResults ()
    {
        return
        [
            new(
                UcliCommandNames.Plan,
                PlanCommandResultFactory.Create(PlanServiceResult.Failure(
                    "Unity startup is blocked.",
                    [CreateStartupFailure()]))),
            new(
                UcliCommandNames.Refresh,
                RefreshCommandResultFactory.Create(OperationExecuteResultFactory.Failure(
                    "request-id",
                    [],
                    [CreateStartupFailure()],
                    "Unity startup is blocked."))),
            new(
                UcliCommandNames.Resolve,
                ResolveCommandResultFactory.Create(ResolveServiceResult.Failure(
                    "request-id",
                    [],
                    [CreateStartupFailure()],
                    "Unity startup is blocked.",
                    CreateReadIndexInfo()))),
            new(
                UcliCommandNames.Query,
                QueryCommandResultFactory.Create(QueryServiceResult.Failure(
                    UcliCommandNames.Query,
                    "request-id",
                    [],
                    [CreateStartupFailure()],
                    "Unity startup is blocked.",
                    CreateReadIndexInfo()))),
            new(
                UcliCommandNames.Compile,
                CompileCommandResultFactory.Create(CompileExecutionResult.Failure(CreateStartupFailure()))),
            new(
                UcliCommandNames.OpsList,
                OpsCommandResultFactory.CreateList(OpsListServiceResult.Failure(
                    "Unity startup is blocked.",
                    DaemonErrorCodes.DaemonStartupBlocked,
                    CreateStartupFailureDetail()))),
            new(
                UcliCommandNames.OpsDescribe,
                OpsCommandResultFactory.CreateDescribe(OpsDescribeServiceResult.Failure(
                    "Unity startup is blocked.",
                    DaemonErrorCodes.DaemonStartupBlocked,
                    CreateStartupFailureDetail()))),
        ];
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenFailureHasStartupDetail_EmitsStartupDiagnosisWithoutChangingEnvelope ()
    {
        foreach (StartupFailureCommandResultCase testCase in StartupFailureCommandResults())
        {
            using var json = JsonDocument.Parse(ResultWriter.Write(testCase.Result));
            var root = json.RootElement;
            JsonAssert.For(root)
                .HasString("command", testCase.CommandName)
                .HasString("status", "error")
                .HasProperty("payload", payload => payload
                    .HasProperty("startup", startup => startup
                        .HasString("startupStatus", "blocked")
                        .HasString("startupBlockingReason", "compile"))
                    .HasProperty("diagnosis", diagnosis => diagnosis
                        .HasString("reason", "unityScriptCompilationFailed")
                        .HasProperty("primaryDiagnostic", primaryDiagnostic => primaryDiagnostic
                            .HasString("code", "CS0246")))
                    .HasString("retryDisposition", "retryAfterFix")
                    .HasBoolean("safeToRetryImmediately", false));
            CommandResultAssert.HasSingleError(root, DaemonErrorCodes.DaemonStartupBlocked);
        }
    }

    private readonly record struct StartupFailureCommandResultCase (
        string CommandName,
        CommandResult Result);

    private static ApplicationFailure CreateStartupFailure ()
    {
        return ApplicationFailure.UnityIpcFailure(
            "Unity startup is blocked.",
            DaemonErrorCodes.DaemonStartupBlocked,
            startupFailure: CreateStartupFailureDetail());
    }

    private static StartupFailureDetail CreateStartupFailureDetail ()
    {
        return new StartupFailureDetail(
            Startup: new DaemonStartupObservationOutput(
                StartupStatus: DaemonStartupStatus.Blocked,
                StartupBlockingReason: DaemonStartupBlockingReason.Compile,
                LaunchAttemptId: null,
                EditorMode: DaemonEditorMode.Batchmode,
                OwnerKind: DaemonSessionOwnerKind.Cli,
                CanShutdownProcess: true,
                ProcessId: 1234,
                StartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                ElapsedMilliseconds: null,
                ProcessAction: DaemonStartupProcessAction.Terminated,
                ProcessTermination: null,
                ArtifactPath: null,
                RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix),
            Diagnosis: new DaemonDiagnosisOutput(
                Reason: "unityScriptCompilationFailed",
                Message: "Unity startup is blocked.",
                ReportedBy: "cli",
                IsInferred: true,
                UpdatedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:06+00:00"),
                ProcessId: 1234,
                EditorInstancePath: null,
                ProcessStartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                UnityLogPath: "/repo/.ucli/local/logs/unity.log",
                StartupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
                ActionRequired: "fixCompileErrors",
                PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                    Kind: "compiler",
                    Code: "CS0246",
                    File: "Assets/Scripts/Broken.cs",
                    Line: 10,
                    Column: 5,
                    Message: "error CS0246")),
            RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
            SafeToRetryImmediately: false);
    }

    private static ReadIndexInfo CreateReadIndexInfo ()
    {
        return new ReadIndexInfo(
            Used: false,
            Hit: false,
            Source: ReadIndexInfoSource.Unity,
            Freshness: IndexFreshness.Probable,
            GeneratedAtUtc: null,
            FallbackReason: null);
    }
}
