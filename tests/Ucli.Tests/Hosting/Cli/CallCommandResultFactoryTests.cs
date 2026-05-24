using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;

namespace MackySoft.Ucli.Tests;

public sealed class CallCommandResultFactoryTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults: [],
                Plan: new CallPlanOutput(
                    RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    Project: ProjectIdentityInfoTestFactory.Create(),
                    OpResults: [],
                    PlanToken: null),
                ReadPostcondition: null)));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, SerializerOptions));
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

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, SerializerOptions));
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

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, SerializerOptions));
        var root = json.RootElement;
        JsonAssert.For(root)
            .HasString("command", UcliCommandNames.Call)
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

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenReadPostconditionExists_EmitsTopLevelPayloadOnly ()
    {
        var readPostcondition = new OperationExecutionReadPostcondition(
        [
            new OperationExecutionReadPostconditionRequirement(
                Surface: IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite,
                MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T01:02:03+00:00"))
            {
                ScenePath = "Assets/Scenes/Main.unity",
            },
        ]);
        var result = CallCommandResultFactory.Create(CallServiceResult.Success(
            new CallExecutionOutput(
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Project: ProjectIdentityInfoTestFactory.Create(),
                OpResults: [],
                Plan: new CallPlanOutput(
                    RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    Project: ProjectIdentityInfoTestFactory.Create(),
                    OpResults: [],
                    PlanToken: "plan-token-1"),
                ReadPostcondition: readPostcondition),
            "uCLI call completed."));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, SerializerOptions));
        var payload = json.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasProperty("readPostcondition", readPostconditionElement => readPostconditionElement
                .HasArrayLength("requirements", 1)
                .HasProperty("requirements", 0, requirement => requirement
                    .HasString("surface", IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite)
                    .HasString("scenePath", "Assets/Scenes/Main.unity")));
        Assert.False(payload.GetProperty("plan").TryGetProperty("readPostcondition", out _));
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
}
