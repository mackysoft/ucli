using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Testing;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunCommandResultFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithFailResult_ReturnsOkEnvelopeWithPayload ()
    {
        var serviceResult = TestRunServiceResult.Fail(
            message: "Unity test execution completed with failed tests.",
            runId: "run-id",
            artifactsDir: "/tmp/artifacts",
            summaryJsonPath: "/tmp/artifacts/summary.json");

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal(1, result.ProtocolVersion);
        Assert.Equal(UcliCommandNames.TestRun, result.Command);
        Assert.Equal("ok", result.Status);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(serviceResult.Message, result.Message);
        Assert.Empty(result.Errors);

        var payload = JsonSerializer.SerializeToElement(result.Payload);
        JsonAssert.For(payload)
            .HasString("result", "fail")
            .IsNull("errorKind")
            .HasString("runId", "run-id")
            .HasString("artifactsDir", "/tmp/artifacts")
            .HasString("summaryJsonPath", "/tmp/artifacts/summary.json");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithServiceErrorCode_ReturnsErrorEnvelopeWithSameCode ()
    {
        UcliErrorCode errorCode = new("UNITY_TEST_EXECUTION_FAILED");
        const string message = "Unity test execution failed.";

        var serviceResult = TestRunServiceResult.ToolError(
            message: message,
            errorCode: errorCode,
            runId: "run-id",
            artifactsDir: "/tmp/artifacts",
            summaryJsonPath: "/tmp/artifacts/summary.json");

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal(UcliCommandNames.TestRun, result.Command);
        Assert.Equal("error", result.Status);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(errorCode, result.Errors[0].Code);
        Assert.Equal(message, result.Errors[0].Message);
        Assert.Null(result.Errors[0].OpId);

        var payload = JsonSerializer.SerializeToElement(result.Payload);
        JsonAssert.For(payload)
            .IsNull("result")
            .HasString("errorKind", "toolError")
            .HasString("runId", "run-id")
            .HasString("artifactsDir", "/tmp/artifacts")
            .HasString("summaryJsonPath", "/tmp/artifacts/summary.json");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithMissingErrorCode_FallsBackToInternalError ()
    {
        var serviceResult = new TestRunServiceResult(
            Result: null,
            ErrorKind: TestRunErrorKind.InfraError,
            Outcome: ApplicationOutcome.InfrastructureError,
            Message: "Unexpected execution pipeline state.",
            RunId: null,
            ArtifactsDir: null,
            SummaryJsonPath: null,
            ErrorCode: null);

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal("error", result.Status);
        Assert.Equal(2, result.ExitCode);
        Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Errors[0].Code);

        var payload = JsonSerializer.SerializeToElement(result.Payload);
        JsonAssert.For(payload)
            .IsNull("result")
            .HasString("errorKind", "infraError")
            .IsNull("runId")
            .IsNull("artifactsDir")
            .IsNull("summaryJsonPath");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithInfrastructureErrorAndInvalidArgumentCode_ReturnsInfrastructureExitCode ()
    {
        const string message = "Daemon session token could not be resolved.";
        var serviceResult = TestRunServiceResult.InfraError(
            message,
            UcliCoreErrorCodes.InvalidArgument,
            runId: "run-id",
            artifactsDir: "/tmp/artifacts",
            summaryJsonPath: "/tmp/artifacts/summary.json");

        var result = TestRunCommandResultFactory.Create(serviceResult);

        Assert.Equal("error", result.Status);
        Assert.Equal(2, result.ExitCode);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Equal(message, error.Message);

        var payload = JsonSerializer.SerializeToElement(result.Payload);
        JsonAssert.For(payload)
            .IsNull("result")
            .HasString("errorKind", "infraError")
            .HasString("runId", "run-id")
            .HasString("artifactsDir", "/tmp/artifacts")
            .HasString("summaryJsonPath", "/tmp/artifacts/summary.json");
    }
}
