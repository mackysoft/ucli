using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunResultMapperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Map_WithIpcTimeoutExecutionFailure_ReturnsToolErrorWithArtifactsContext ()
    {
        var session = CreateSession();
        var mapper = new TestRunResultMapper();

        var result = mapper.Map(TestRunExecutionPipelineResult.Success(
            session,
            UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.IpcTimedOut,
                "Unity daemon test run request timed out."),
            UnityResultsConversionResult.Success(false)));

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Equal(ApplicationFailureKind.Timeout, result.Failure!.Kind);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WithCompletionErrorAfterFailedTests_PrefersFailedTestsOutcome ()
    {
        var session = CreateSession();
        var mapper = new TestRunResultMapper();

        var result = mapper.Map(TestRunExecutionPipelineResult.Failure(
            ExecutionError.InternalError("Failed to finalize artifacts."),
            session,
            UnityTestExecutionResult.Success(0),
            UnityResultsConversionResult.Success(true)));

        Assert.Equal(TestRunResultKind.Fail, result.Result);
        Assert.Null(result.ErrorKind);
        Assert.Equal(ApplicationOutcome.TestFailure, result.Outcome);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WithPipelineErrorAndSession_PreservesArtifactsContext ()
    {
        var session = CreateSession();
        var mapper = new TestRunResultMapper();

        var result = mapper.Map(TestRunExecutionPipelineResult.Failure(
            ExecutionError.InternalError("Unexpected execution pipeline state."),
            session));

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(ApplicationFailureKind.ExternalProcessFailure, result.Failure!.Kind);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    private static ArtifactsSession CreateSession ()
    {
        return new ArtifactsSession(
            "run-id",
            TestArtifactPaths.Create("/tmp/ucli-tests/run-id"),
            DateTimeOffset.Parse("2026-04-21T00:00:00Z"));
    }
}
