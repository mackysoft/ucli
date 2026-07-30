using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;
using static MackySoft.Ucli.Application.Tests.TestRunServiceTestFactory;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunResultMapperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Map_WithIpcTimeoutExecutionFailure_ReturnsCommandErrorWithArtifactsContext ()
    {
        var session = CreateSession();
        var mapper = new TestRunResultMapper();
        var failure = ApplicationFailure.Create(
            ApplicationFailureKind.ExternalProcessFailure,
            "Unity daemon test run request timed out.",
            ExecutionErrorCodes.IpcTimeout,
            instancePath: null,
            outcome: ApplicationOutcome.InfrastructureError,
            startupFailure: null);

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(mapper.Map(
            TestRunExecutionPipelineResult.FailedAfterArtifacts(
            session,
            failure)));

        Assert.Equal(failure, result.PrimaryFailure);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WithStartupFailureExecutionFailure_PreservesStartupDetailInFailure ()
    {
        var session = CreateSession();
        var startupFailure = new StartupFailureDetail(
            Startup: null,
            Diagnosis: null,
            RetryDisposition: DaemonStartupRetryDisposition.Unknown,
            SafeToRetryImmediately: false);
        var mapper = new TestRunResultMapper();
        var failure = ApplicationFailure.Create(
            ApplicationFailureKind.ExternalProcessFailure,
            "Unity startup is blocked.",
            DaemonErrorCodes.DaemonStartupBlocked,
            instancePath: null,
            outcome: ApplicationOutcome.InfrastructureError,
            startupFailure);

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(mapper.Map(
            TestRunExecutionPipelineResult.FailedAfterArtifacts(
            session,
            failure)));

        Assert.Equal(startupFailure, result.PrimaryFailure.StartupFailure);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WithPrimaryAndFinalizationFailures_PreservesBothFailures ()
    {
        var session = CreateSession();
        var mapper = new TestRunResultMapper();
        var primaryFailure = ApplicationFailure.InvalidInput(
            "The normalized test result is invalid.",
            TestRunErrorCodes.TestResultsXmlInvalid,
            instancePath: null,
            startupFailure: null);
        var finalizationFailure = ApplicationFailure.InternalError(
            "Failed to finalize artifacts.",
            UcliCoreErrorCodes.InternalError,
            instancePath: null,
            startupFailure: null);

        var result = Assert.IsType<TestRunAfterCreationCommandErrorWithFinalizationServiceResult>(mapper.Map(
            TestRunExecutionPipelineResult.FailedAfterArtifactsWithFinalizationFailure(
            session,
            primaryFailure,
            finalizationFailure)));

        Assert.Equal([primaryFailure, finalizationFailure], result.Failures);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WithIncompleteConversion_ReturnsIncompleteWithArtifactsContext ()
    {
        var session = CreateSession();
        var mapper = new TestRunResultMapper();

        var result = Assert.IsType<TestRunCompletedServiceResult>(mapper.Map(TestRunExecutionPipelineResult.Completed(
            session,
            TestRunResultTestValues.CreateConversion(Verdict.Incomplete))));

        Assert.Equal(Verdict.Incomplete, result.Verdict);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WithPassConversionAndNoReportedTestCases_ReturnsPass ()
    {
        var session = CreateSession();
        var mapper = new TestRunResultMapper();

        var result = Assert.IsType<TestRunCompletedServiceResult>(mapper.Map(TestRunExecutionPipelineResult.Completed(
            session,
            TestRunResultTestValues.CreateConversion(Verdict.Pass))));

        Assert.Equal(Verdict.Pass, result.Verdict);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Map_WithPipelineErrorAndSession_ReturnsCommandErrorWithArtifactsContext ()
    {
        var session = CreateSession();
        var mapper = new TestRunResultMapper();
        var failure = ApplicationFailure.InternalError(
            "Unexpected execution pipeline state.",
            UcliCoreErrorCodes.InternalError,
            instancePath: null,
            startupFailure: null);

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(mapper.Map(
            TestRunExecutionPipelineResult.FailedAfterArtifacts(
            session,
            failure)));

        Assert.Equal(failure, result.PrimaryFailure);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
    }

    private static ArtifactsSession CreateSession ()
    {
        return new ArtifactsSession(
            RunId,
            TestArtifactPaths.Create(Path.Combine(Path.GetTempPath(), "ucli-tests", "run-id")),
            DateTimeOffset.Parse("2026-04-21T00:00:00Z"));
    }
}
