using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.TestRunServiceTestFactory;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunServiceConversionFailureTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenArtifactsCompletionFailsAfterConversionFailure_PreservesPrimaryConversionError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Failure(ExecutionError.InternalError("completion failed"))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.ResultsXmlReadFailed,
                "Failed to read results.xml."))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.TestResultsXmlReadFailed, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenArtifactsCompletionFailsAfterFailedTests_PreservesFailResult ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Failure(ExecutionError.InternalError("completion failed"))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(hasFailedTests: true))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Equal(TestRunResultKind.Fail, result.Result);
        Assert.Null(result.ErrorKind);
        Assert.Equal(ApplicationOutcome.TestFailure, result.Outcome);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithConversionOutputWriteFailure_ReturnsInfraError ()
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
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.OutputWriteFailed,
                "Failed to write results artifacts."))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.TestResultsOutputWriteFailed, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithConversionResultsXmlReadFailure_ReturnsInfraError ()
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
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.ResultsXmlReadFailed,
                "Failed to read results.xml."))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.TestResultsXmlReadFailed, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnexpectedConversionException_ReturnsInfraError ()
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
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => throw new InvalidOperationException("boom")));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnexpectedConversionExceptionAndCompletionFailure_PreservesConversionError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Failure(ExecutionError.InternalError("completion failed"))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => throw new InvalidOperationException("boom")));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal("Unexpected error during Unity results conversion: boom", result.Message);
        Assert.Equal(session.RunId, result.RunId);
    }
}
