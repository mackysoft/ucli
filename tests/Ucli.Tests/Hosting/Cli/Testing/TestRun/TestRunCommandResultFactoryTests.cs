using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Hosting.Cli.Testing;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCommandResultFactoryTests
{
    private static readonly AbsolutePath ArtifactsDirectory = AbsolutePath.Parse(
        Path.Combine(Path.GetTempPath(), "ucli-test-run-result-factory", "artifacts"));
    private static readonly AbsolutePath SummaryJsonPath = AbsolutePath.Resolve(
        ArtifactsDirectory,
        "summary.json");
    private static readonly ArtifactsSession TestArtifactsSession = TestArtifactPaths.CreateSession(
        RunIdTestValues.Test,
        ArtifactsDirectory.Value);

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithFailResult_ReturnsOkEnvelopeWithPayload ()
    {
        var serviceResult = TestRunResultTestValues.CreateCompleted(
            Verdict.Fail,
            TestArtifactsSession);

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal(1, result.ProtocolVersion);
        Assert.Equal(UcliCommandNames.TestRun, result.Command);
        Assert.Equal(CommandResultStatus.Ok, result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(serviceResult.Message, result.Message);
        Assert.Empty(result.Errors);

        var payload = SerializePayload(result);
        JsonAssert.For(payload)
            .HasString("state", TextVocabulary.GetText(TestRunCompletedState.Completed))
            .HasString("verdict", TextVocabulary.GetText(Verdict.Fail))
            .HasString("runId", RunIdTestValues.TestText)
            .HasString("artifactsDir", ArtifactsDirectory.Value)
            .HasString("summaryJsonPath", SummaryJsonPath.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithIncompleteResult_ReturnsOkEnvelopeWithIncompleteVerdict ()
    {
        var serviceResult = TestRunResultTestValues.CreateCompleted(
            Verdict.Incomplete,
            TestArtifactsSession);

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal(CommandResultStatus.Ok, result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Errors);
        JsonAssert.For(SerializePayload(result))
            .HasString("state", TextVocabulary.GetText(TestRunCompletedState.Completed))
            .HasString("verdict", TextVocabulary.GetText(Verdict.Incomplete));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithCommandErrorAfterRunCreation_EmitsErrorEnvelopeWithRunContext ()
    {
        var errorCode = TestRunErrorCodes.UnityTestExecutionFailed;
        const string message = "Unity test execution failed.";
        var serviceResult = TestRunServiceResult.AfterRunCreationError(
            ApplicationFailure.Create(
                ApplicationFailureKind.ExternalProcessFailure,
                message,
                errorCode,
                instancePath: null,
                ApplicationOutcome.InfrastructureError,
                startupFailure: null),
            TestArtifactsSession);

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal(CommandResultStatus.Error, result.Status);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.Equal(errorCode, Assert.Single(result.Errors).Code);
        var payload = SerializePayload(result);
        JsonAssert.For(payload)
            .HasString("errorKind", TextVocabulary.GetText(TestRunErrorKind.InfraError));
        var run = payload.GetProperty("run");
        JsonAssert.For(run)
            .HasString("runId", RunIdTestValues.TestText)
            .HasString("artifactsDir", ArtifactsDirectory.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithServiceErrorCode_ReturnsErrorEnvelopeWithSameCode ()
    {
        var errorCode = TestRunErrorCodes.UnityTestExecutionFailed;
        const string message = "Unity test execution failed.";

        var serviceResult = TestRunServiceResult.ToolError(
            ApplicationFailure.Create(
                ApplicationFailureKind.ExternalProcessFailure,
                message,
                errorCode,
                instancePath: null,
                ApplicationOutcome.ToolError,
                startupFailure: null));

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal(UcliCommandNames.TestRun, result.Command);
        Assert.Equal(CommandResultStatus.Error, result.Status);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(errorCode, result.Errors[0].Code);
        Assert.Equal(message, result.Errors[0].Message);
        Assert.Null(result.Errors[0].InstancePath);

        var payload = SerializePayload(result);
        JsonAssert.For(payload)
            .HasString("errorKind", TextVocabulary.GetText(TestRunErrorKind.ToolError));
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("run").ValueKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithStartupFailure_PreservesStructuredStartupDetail ()
    {
        var startupFailure = ReadyCommandTestData.CreateStartupFailureDetail();
        var serviceResult = TestRunServiceResult.ToolError(
            ApplicationFailure.Create(
                ApplicationFailureKind.ExternalProcessFailure,
                "Unity startup failed.",
                TestRunErrorCodes.UnityTestExecutionFailed,
                instancePath: null,
                ApplicationOutcome.ToolError,
                startupFailure));

        var result = TestRunCommandResultFactory.Create(serviceResult);

        var startupDetail = SerializePayload(result).GetProperty("startupFailure");
        JsonAssert.For(startupDetail)
            .HasProperty("startup", startup => startup
                .HasString("startupStatus", TextVocabulary.GetText(DaemonStartupStatus.Blocked))
                .HasString("startupBlockingReason", TextVocabulary.GetText(DaemonStartupBlockingReason.Compile)))
            .HasProperty("diagnosis", diagnosis => diagnosis
                .HasString("reason", TextVocabulary.GetText(DaemonDiagnosisReason.UnityScriptCompilationFailed))
                .HasProperty("primaryDiagnostic", diagnostic => diagnostic
                    .HasString("code", "CS0246")))
            .HasString(
                "retryDisposition",
                TextVocabulary.GetText(DaemonStartupRetryDisposition.RetryAfterFix));
        Assert.False(startupDetail.GetProperty("safeToRetryImmediately").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithInfrastructureErrorAndInvalidArgumentCode_ReturnsToolErrorExitCode ()
    {
        const string message = "Daemon session token could not be resolved.";
        var serviceResult = TestRunServiceResult.InfraError(
            message,
            UcliCoreErrorCodes.InvalidArgument);

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal(CommandResultStatus.Error, result.Status);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Equal(message, error.Message);

        var payload = SerializePayload(result);
        JsonAssert.For(payload)
            .HasString("errorKind", TextVocabulary.GetText(TestRunErrorKind.InfraError));
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("run").ValueKind);
    }

    private static JsonElement SerializePayload (CommandResult result)
    {
        return JsonSerializer.SerializeToElement(
            result.Payload,
            CliOutputJsonSerializerOptions.Default);
    }

}
