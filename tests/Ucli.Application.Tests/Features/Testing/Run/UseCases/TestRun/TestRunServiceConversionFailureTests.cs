using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Testing;
using static MackySoft.Ucli.Application.Tests.TestRunServiceTestFactory;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunServiceConversionFailureTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenArtifactsCompletionFailsAfterConversionFailure_PreservesBothCommandErrors ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Failure(
                    ExecutionError.InternalError("completion failed", UcliCoreErrorCodes.InternalError))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversionFailure(
                UnityResultsConversionFailureKind.ResultsXmlReadFailed,
                "Failed to read results.xml."))));

        var result = Assert.IsType<TestRunAfterCreationCommandErrorWithFinalizationServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(TestRunErrorCodes.TestResultsXmlReadFailed, result.PrimaryFailure.Code);
        Assert.Equal("Failed to read results.xml.", result.PrimaryFailure.Message);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Failures[1].Code);
        Assert.Equal("completion failed", result.Failures[1].Message);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenArtifactsCompletionFailsAfterNormalizedResults_ReturnsCommandErrorWithoutVerdict ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Failure(
                    ExecutionError.InternalError("completion failed", UcliCoreErrorCodes.InternalError))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Fail))));

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationFailureKind.InternalError, result.PrimaryFailure.Kind);
        Assert.Equal(ApplicationOutcome.ToolError, result.PrimaryFailure.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.PrimaryFailure.Code);
        Assert.Equal("completion failed", result.Message);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithConversionOutputWriteFailure_ReturnsFailedRun ()
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
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversionFailure(
                UnityResultsConversionFailureKind.OutputWriteFailed,
                "Failed to write results artifacts."))));

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.PrimaryFailure.Outcome);
        Assert.Equal(TestRunErrorCodes.TestResultsOutputWriteFailed, result.PrimaryFailure.Code);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithConversionResultsXmlReadFailure_ReturnsFailedRun ()
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
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversionFailure(
                UnityResultsConversionFailureKind.ResultsXmlReadFailed,
                "Failed to read results.xml."))));

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.PrimaryFailure.Outcome);
        Assert.Equal(TestRunErrorCodes.TestResultsXmlReadFailed, result.PrimaryFailure.Code);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnexpectedConversionException_ReturnsFailedRun ()
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
            resultsConverter: new StubUnityResultsConverter(_ => throw new InvalidOperationException("boom")));

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationFailureKind.InternalError, result.PrimaryFailure.Kind);
        Assert.Equal(ApplicationOutcome.ToolError, result.PrimaryFailure.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.PrimaryFailure.Code);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnexpectedConversionExceptionAndCompletionFailure_PreservesPrimaryConversionError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Failure(
                    ExecutionError.InternalError("completion failed", UcliCoreErrorCodes.InternalError))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => throw new InvalidOperationException("boom")));

        var result = Assert.IsType<TestRunAfterCreationCommandErrorWithFinalizationServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationFailureKind.InternalError, result.PrimaryFailure.Kind);
        Assert.Equal(ApplicationOutcome.ToolError, result.PrimaryFailure.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.PrimaryFailure.Code);
        Assert.Equal("Unexpected error during Unity results conversion: boom", result.Message);
        Assert.Equal("completion failed", result.Failures[1].Message);
        Assert.Equal(session.RunId, result.RunId);
    }
}
