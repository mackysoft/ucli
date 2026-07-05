using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using static MackySoft.Ucli.Application.Tests.TestRunServiceTestFactory;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunServiceResultProjectionTests
{
    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(false, (int)TestRunResultKind.Pass, (int)ApplicationOutcome.Success)]
    [InlineData(true, (int)TestRunResultKind.Fail, (int)ApplicationOutcome.TestFailure)]
    public async Task Execute_WithSuccessfulExecution_ReturnsPassOrFail (
        bool hasFailedTests,
        int expectedResult,
        int expectedOutcome)
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(hasFailedTests))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Equal((TestRunResultKind)expectedResult, result.Result);
        Assert.Null(result.ErrorKind);
        Assert.Equal((ApplicationOutcome)expectedOutcome, result.Outcome);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("NoSuchTestName", null)]
    [InlineData(null, "NoSuchCategory")]
    public async Task Execute_WithFilteredEmptyRun_ReturnsNoTestsExecutedInvalidInput (
        string? testFilter,
        string? testCategory)
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();
        var input = CreateInput() with
        {
            TestFilter = testFilter,
            TestCategory = testCategory is null ? null : [testCategory],
        };

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(
                hasFailedTests: false,
                reportedTestCaseCount: 0))));

        var result = await service.ExecuteAsync(input, cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InvalidInput, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Equal(TestRunErrorCodes.TestRunNoTestsExecuted, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }
}
