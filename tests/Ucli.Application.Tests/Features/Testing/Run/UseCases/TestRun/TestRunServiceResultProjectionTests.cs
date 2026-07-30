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
    [MemberData(nameof(GetVerdicts))]
    public async Task Execute_WithSuccessfulExecution_ReturnsConversionVerdict (
        Verdict expectedVerdict)
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(
                TestRunResultTestValues.CreateConversion(expectedVerdict))));

        var result = Assert.IsType<TestRunCompletedServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(expectedVerdict, result.Verdict);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    public static TheoryData<Verdict> GetVerdicts ()
    {
        return new TheoryData<Verdict>
        {
            Verdict.Pass,
            Verdict.Fail,
            Verdict.Incomplete,
        };
    }
}
