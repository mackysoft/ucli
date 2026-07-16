using System.Text.Json;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using static MackySoft.Ucli.Application.Tests.TestRunServiceTestFactory;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunServiceProgressTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithProgressSink_EmitsRunStartedAndForwardsUnityProgress ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();
        var progressSink = new CollectingCommandProgressSink();
        var unityProgressFrames = new[]
        {
            new UnityRequestProgressFrame(
                TestRunProgressEventNames.RunStarted,
                IpcPayloadCodec.SerializeToElement(new TestRunStartedEntry(
                    session.RunId,
                    "editmode",
                    null,
                    ["MyGame.Tests"],
                    ["smoke"]))),
            new UnityRequestProgressFrame(
                TestRunProgressEventNames.CaseStarted,
                IpcPayloadCodec.SerializeToElement(new TestCaseStartedEntry(
                    session.RunId,
                    "test-id",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"]))),
            new UnityRequestProgressFrame(
                TestRunProgressEventNames.CaseFinished,
                IpcPayloadCodec.SerializeToElement(new TestCaseFinishedEntry(
                    session.RunId,
                    "test-id",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"],
                    TestCaseResult.Pass,
                    42,
                    null,
                    null))),
            new UnityRequestProgressFrame(
                TestRunProgressEventNames.RunDiagnostic,
                IpcPayloadCodec.SerializeToElement(new TestRunDiagnosticEntry(
                    session.RunId,
                    new UcliCode("TEST_PROGRESS_STUB"),
                    "stub progress",
                    UcliDiagnosticSeverity.Info))),
        };

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            streamingProgressFrames: unityProgressFrames);

        var result = await service.ExecuteAsync(CreateInput(), progressSink, CancellationToken.None);

        Assert.Equal(TestRunResultKind.Pass, result.Result);
        TestRunProgressAssert.RunStartedAndUnityProgressForwarded(progressSink, session.RunId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnsupportedUnityProgressEvent_ReturnsToolError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();
        var progressSink = new CollectingCommandProgressSink();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            streamingProgressFrame: new UnityRequestProgressFrame(
                "test.run.unsupported",
                IpcPayloadCodec.SerializeToElement(new { runId = session.RunId })));

        var result = await service.ExecuteAsync(CreateInput(), progressSink, CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionFailed, result.ErrorCode);
        Assert.Contains("Unity test-run progress event is not supported", result.Message, StringComparison.Ordinal);
        TestRunProgressAssert.RejectedUnityProgressStoppedAfterRunStarted(progressSink);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithMismatchedUnityProgressRunId_ReturnsToolError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();
        var progressSink = new CollectingCommandProgressSink();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            streamingProgressFrame: new UnityRequestProgressFrame(
                TestRunProgressEventNames.CaseStarted,
                IpcPayloadCodec.SerializeToElement(new TestCaseStartedEntry(
                    OtherRunId,
                    "test-id",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    []))));

        var result = await service.ExecuteAsync(CreateInput(), progressSink, CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionFailed, result.ErrorCode);
        Assert.Contains("runId mismatch", result.Message, StringComparison.Ordinal);
        TestRunProgressAssert.RejectedUnityProgressStoppedAfterRunStarted(progressSink);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithInvalidUnityProgressPayload_ReturnsToolError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();
        var progressSink = new CollectingCommandProgressSink();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            streamingProgressFrame: new UnityRequestProgressFrame(
                TestRunProgressEventNames.CaseFinished,
                JsonDocument.Parse("[]").RootElement.Clone()));

        var result = await service.ExecuteAsync(CreateInput(), progressSink, CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionFailed, result.ErrorCode);
        Assert.Contains("progress payload is invalid", result.Message, StringComparison.Ordinal);
        TestRunProgressAssert.RejectedUnityProgressStoppedAfterRunStarted(progressSink);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithContractInvalidUnityProgressPayload_ReturnsToolError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();
        var progressSink = new CollectingCommandProgressSink();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            streamingProgressFrame: new UnityRequestProgressFrame(
                TestRunProgressEventNames.CaseFinished,
                IpcPayloadCodec.SerializeToElement(new
                {
                    runId = session.RunId,
                    testId = "test-id",
                    testName = "SmokeTest.Passes",
                    assemblyName = "MyGame.Tests",
                    testPlatform = "editmode",
                    categories = new[] { "smoke" },
                    result = "unknown",
                    durationMilliseconds = 42,
                    message = (string?)null,
                    stackTrace = (string?)null,
                })));

        var result = await service.ExecuteAsync(CreateInput(), progressSink, CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionFailed, result.ErrorCode);
        Assert.Contains("progress payload is invalid", result.Message, StringComparison.Ordinal);
        TestRunProgressAssert.RejectedUnityProgressStoppedAfterRunStarted(progressSink);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunProgressPayloadValidator_WithMismatchedEventAndPayloadType_ThrowsProtocolViolation ()
    {
        var exception = Assert.Throws<TestRunProgressProtocolException>(() =>
            TestRunProgressPayloadValidator.Validate(
                TestRunProgressEventNames.CaseStarted,
                new TestRunDiagnosticEntry(
                    RunId,
                    new UcliCode("TEST_PROGRESS_STUB"),
                    "stub progress",
                    UcliDiagnosticSeverity.Info),
                RunId));

        Assert.Contains("payload type violates contract", exception.Message, StringComparison.Ordinal);
        Assert.Contains(TestRunProgressEventNames.CaseStarted, exception.Message, StringComparison.Ordinal);
    }
}
