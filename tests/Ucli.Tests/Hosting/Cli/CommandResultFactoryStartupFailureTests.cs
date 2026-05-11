using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Hosting.Cli.Requests;

namespace MackySoft.Ucli.Tests;

public sealed class CommandResultFactoryStartupFailureTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static IEnumerable<object[]> StartupFailureCommandResults
    {
        get
        {
            yield return
            [
                UcliCommandNames.Plan,
                PlanCommandResultFactory.Create(PlanServiceResult.Failure(
                    "Unity startup is blocked.",
                    [CreateStartupFailure()]))
            ];
            yield return
            [
                UcliCommandNames.Refresh,
                RefreshCommandResultFactory.Create(OperationExecuteResultFactory.Failure(
                    "request-id",
                    [],
                    [CreateStartupFailure()],
                    "Unity startup is blocked."))
            ];
            yield return
            [
                UcliCommandNames.Resolve,
                ResolveCommandResultFactory.Create(ResolveServiceResult.Failure(
                    "request-id",
                    [],
                    [CreateStartupFailure()],
                    "Unity startup is blocked.",
                    CreateReadIndexInfo()))
            ];
            yield return
            [
                UcliCommandNames.Query,
                QueryCommandResultFactory.Create(QueryServiceResult.Failure(
                    UcliCommandNames.Query,
                    "request-id",
                    [],
                    [CreateStartupFailure()],
                    "Unity startup is blocked.",
                    CreateReadIndexInfo()))
            ];
            yield return
            [
                UcliCommandNames.OpsList,
                OpsCommandResultFactory.CreateList(OpsListServiceResult.Failure(
                    "Unity startup is blocked.",
                    DaemonErrorCodes.DaemonStartupBlocked,
                    CreateStartupFailureDetail()))
            ];
            yield return
            [
                UcliCommandNames.OpsDescribe,
                OpsCommandResultFactory.CreateDescribe(OpsDescribeServiceResult.Failure(
                    "Unity startup is blocked.",
                    DaemonErrorCodes.DaemonStartupBlocked,
                    CreateStartupFailureDetail()))
            ];
        }
    }

    [Theory]
    [MemberData(nameof(StartupFailureCommandResults))]
    [Trait("Size", "Small")]
    public void Create_WhenFailureHasStartupDetail_EmitsStartupDiagnosisWithoutChangingEnvelope (
        string commandName,
        object resultObject)
    {
        var result = Assert.IsType<CommandResult>(resultObject);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, SerializerOptions));
        var root = json.RootElement;
        JsonAssert.For(root)
            .HasString("command", commandName)
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
                StartupStatus: "blocked",
                StartupBlockingReason: "compile",
                LaunchAttemptId: null,
                EditorMode: "batchmode",
                OwnerKind: "cli",
                CanShutdownProcess: true,
                ProcessId: 1234,
                StartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                ElapsedMilliseconds: null,
                ProcessAction: "terminated",
                ProcessTermination: null,
                ArtifactPath: null,
                RetryDisposition: "retryAfterFix"),
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
                StartupPhase: "scriptCompilation",
                ActionRequired: "fixCompileErrors",
                PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                    Kind: "compiler",
                    Code: "CS0246",
                    File: "Assets/Scripts/Broken.cs",
                    Line: 10,
                    Column: 5,
                    Message: "error CS0246")),
            RetryDisposition: "retryAfterFix",
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
