using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Testing;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCommandResultFactoryTests
{
    private static readonly CommandResultJsonContractWriter ResultWriter = new();
    private static readonly AbsolutePath ArtifactsDirectory = AbsolutePath.Parse(
        Path.Combine(Path.GetTempPath(), "ucli-test-run-result-factory", "artifacts"));
    private static readonly AbsolutePath SummaryJsonPath = AbsolutePath.Resolve(
        ArtifactsDirectory,
        "summary.json");

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithFailResult_ReturnsOkEnvelopeWithPayload ()
    {
        var serviceResult = TestRunServiceResult.Fail(
            message: "Unity test execution completed with failed tests.",
            runId: RunIdTestValues.Test,
            artifactsDir: ArtifactsDirectory,
            summaryJsonPath: SummaryJsonPath);

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal(1, result.ProtocolVersion);
        Assert.Equal(UcliCommandNames.TestRun, result.Command);
        Assert.Equal(CommandResultStatus.Ok, result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(serviceResult.Message, result.Message);
        Assert.Empty(result.Errors);

        using var json = JsonDocument.Parse(ResultWriter.Write(result));
        var payload = json.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("result", "fail")
            .IsNull("errorKind")
            .HasString("runId", RunIdTestValues.TestText)
            .HasString("artifactsDir", ArtifactsDirectory.Value)
            .HasString("summaryJsonPath", SummaryJsonPath.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithServiceErrorCode_ReturnsErrorEnvelopeWithSameCode ()
    {
        UcliCode errorCode = new("UNITY_TEST_EXECUTION_FAILED");
        const string message = "Unity test execution failed.";

        var serviceResult = TestRunServiceResult.ToolError(
            message: message,
            errorCode: errorCode,
            runId: RunIdTestValues.Test,
            artifactsDir: ArtifactsDirectory,
            summaryJsonPath: SummaryJsonPath);

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal(UcliCommandNames.TestRun, result.Command);
        Assert.Equal(CommandResultStatus.Error, result.Status);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(errorCode, result.Errors[0].Code);
        Assert.Equal(message, result.Errors[0].Message);
        Assert.Null(result.Errors[0].OpId);

        using var json = JsonDocument.Parse(ResultWriter.Write(result));
        var payload = json.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .IsNull("result")
            .HasString("errorKind", "toolError")
            .HasString("runId", RunIdTestValues.TestText)
            .HasString("artifactsDir", ArtifactsDirectory.Value)
            .HasString("summaryJsonPath", SummaryJsonPath.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithStartupFailure_EmitsStartupDiagnosisInErrorPayload ()
    {
        var serviceResult = TestRunServiceResult.ToolError(
            message: "Unity startup is blocked.",
            errorCode: DaemonErrorCodes.DaemonStartupBlocked,
            runId: RunIdTestValues.Test,
            artifactsDir: ArtifactsDirectory,
            summaryJsonPath: SummaryJsonPath,
            startupFailure: CreateStartupFailureDetail());

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal(UcliCommandNames.TestRun, result.Command);
        Assert.Equal(CommandResultStatus.Error, result.Status);
        Assert.Single(result.Errors);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.Errors[0].Code);
        Assert.NotNull(serviceResult.Failure!.StartupFailure);

        using var json = JsonDocument.Parse(ResultWriter.Write(result));
        var payload = json.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .IsNull("result")
            .HasString("errorKind", "toolError")
            .HasProperty("startup", startup => startup
                .HasString("startupStatus", "blocked")
                .HasString("startupBlockingReason", "compile"))
            .HasProperty("diagnosis", diagnosis => diagnosis
                .HasString("reason", "unityScriptCompilationFailed"))
            .HasString("retryDisposition", "retryAfterFix")
            .HasBoolean("safeToRetryImmediately", false);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InfraError_WithNullErrorCode_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => TestRunServiceResult.InfraError(
            "Unexpected execution pipeline state.",
            null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithInfrastructureErrorAndInvalidArgumentCode_ReturnsInfrastructureExitCode ()
    {
        const string message = "Daemon session token could not be resolved.";
        var serviceResult = TestRunServiceResult.InfraError(
            message,
            UcliCoreErrorCodes.InvalidArgument,
            runId: RunIdTestValues.Test,
            artifactsDir: ArtifactsDirectory,
            summaryJsonPath: SummaryJsonPath);

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal(CommandResultStatus.Error, result.Status);
        Assert.Equal(2, result.ExitCode);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Equal(message, error.Message);

        using var json = JsonDocument.Parse(ResultWriter.Write(result));
        var payload = json.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .IsNull("result")
            .HasString("errorKind", "infraError")
            .HasString("runId", RunIdTestValues.TestText)
            .HasString("artifactsDir", ArtifactsDirectory.Value)
            .HasString("summaryJsonPath", SummaryJsonPath.Value);
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
                ProcessId: null,
                StartedAtUtc: null,
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
                ProcessId: null,
                EditorInstancePath: null,
                ProcessStartedAtUtc: null,
                UnityLogPath: "/tmp/artifacts/editor.log",
                StartupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
                ActionRequired: DaemonDiagnosisActionRequired.FixCompileErrors,
                PrimaryDiagnostic: null),
            RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
            SafeToRetryImmediately: false);
    }
}
