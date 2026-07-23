using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Requests;

namespace MackySoft.Ucli.Tests;

public sealed class CallCommandResultFactoryTests
{
    private static readonly CommandResultJsonContractWriter ResultWriter = new();

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenPlanTokenIsMissing_OmitsPlanTokenFromNestedPayload ()
    {
        var result = CallCommandResultFactory.Create(CallServiceResult.Failure(
            "Call failed.",
            [
                ApplicationFailure.InternalError("Call failed."),
            ],
            new CallExecutionOutput(
                requestId: Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62"),
                project: ProjectIdentityInfoTestFactory.Create(),
                opResults: [],
                plan: new CallPlanOutput(
                    opResults: [],
                    planToken: null),
                readPostcondition: null)));

        using var json = JsonDocument.Parse(ResultWriter.Write(result));
        var payload = json.RootElement.GetProperty("payload");
        Assert.True(payload.TryGetProperty("plan", out var planElement));
        Assert.False(planElement.TryGetProperty("planToken", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenOutputIsMissing_UsesEmptyPayload ()
    {
        var result = CallCommandResultFactory.Create(CallServiceResult.Failure(
            "Call failed.",
            [
                ApplicationFailure.InternalError("Call failed."),
            ]));

        using var json = JsonDocument.Parse(ResultWriter.Write(result));
        Assert.False(json.RootElement.GetProperty("payload").EnumerateObject().MoveNext());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenFailureHasStartupDetail_EmitsStartupDiagnosisWithoutChangingEnvelope ()
    {
        var startupFailure = CreateStartupFailureDetail();
        var result = CallCommandResultFactory.Create(CallServiceResult.Failure(
            "Unity startup is blocked.",
            [
                ApplicationFailure.UnityIpcFailure(
                    "Unity startup is blocked.",
                    DaemonErrorCodes.DaemonStartupBlocked,
                    startupFailure: startupFailure),
            ]));

        using var json = JsonDocument.Parse(ResultWriter.Write(result));
        var root = json.RootElement;
        JsonAssert.For(root)
            .HasString("command", UcliCommandNames.Call)
            .HasString("status", TextVocabulary.GetText(CommandResultStatus.Error))
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

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenReadPostconditionExists_EmitsTopLevelPayloadOnly ()
    {
        var readPostcondition = ReadPostconditionTestFactory.CreateSceneTreeLite();
        var result = CallCommandResultFactory.Create(CallServiceResult.Success(
            new CallExecutionOutput(
                requestId: Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62"),
                project: ProjectIdentityInfoTestFactory.Create(),
                opResults: [],
                plan: new CallPlanOutput(
                    opResults: [],
                    planToken: "plan-token-1"),
                readPostcondition: readPostcondition),
            "uCLI call completed."));

        using var json = JsonDocument.Parse(ResultWriter.Write(result));
        var payload = json.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasProperty("readPostcondition", readPostconditionElement => readPostconditionElement
                .HasArrayLength("requirements", 1)
                .HasProperty("requirements", 0, requirement => requirement
                    .HasString("surface", TextVocabulary.GetText(IpcExecuteReadPostconditionSurface.SceneTreeLite))
                    .HasString("scenePath", "Assets/Scenes/Main.unity")));
        Assert.False(payload.GetProperty("plan").TryGetProperty("readPostcondition", out _));
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
                Reason: DaemonDiagnosisReason.UnityScriptCompilationFailed,
                Message: "Unity startup is blocked.",
                ReportedBy: DaemonDiagnosisReportedBy.Cli,
                IsInferred: true,
                UpdatedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:06+00:00"),
                ProcessId: 1234,
                EditorInstancePath: null,
                ProcessStartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                UnityLogPath: "/repo/.ucli/local/logs/unity.log",
                StartupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
                ActionRequired: DaemonDiagnosisActionRequired.FixCompileErrors,
                PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                    Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                    Code: "CS0246",
                    File: "Assets/Scripts/Broken.cs",
                    Line: 10,
                    Column: 5,
                    Message: "error CS0246")),
            RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
            SafeToRetryImmediately: false);
    }
}
