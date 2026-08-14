using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using static MackySoft.Ucli.Application.Tests.TestRunServiceTestFactory;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunServiceOneshotExecutionTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAutoModeFallsBackToOneshotAndExecutionTimesOut_ReturnsExecutionTimeoutErrorCode ()
    {
        using var scope = CreateArtifactsScope();
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(scope.GetPath("artifacts"));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.ProcessTimedOut(
                    "Unity process timed out after 30000 milliseconds.",
                    TestRunErrorCodes.UnityTestExecutionTimeout))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass))));

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.PrimaryFailure.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionTimeout, result.PrimaryFailure.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenOneshotResponseReportsIpcTimeout_PreservesReportedErrorCode ()
    {
        using var scope = CreateArtifactsScope();
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(scope.GetPath("artifacts"));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass))),
            unityRequestResponse: CreateFailureUnityRequestResponse(
                IpcTransportErrorCodes.IpcTimeout,
                "Unity test run timed out after 30000 milliseconds."));

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.PrimaryFailure.Outcome);
        Assert.Equal(IpcTransportErrorCodes.IpcTimeout, result.PrimaryFailure.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenOneshotResponseRejectsInvalidInput_ReturnsInvalidInputCommandError ()
    {
        using var scope = CreateArtifactsScope();
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(scope.GetPath("artifacts"));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(
                TestRunResultTestValues.CreateConversion(Verdict.Pass))),
            unityRequestResponse: CreateFailureUnityRequestResponse(
                UcliCoreErrorCodes.InvalidArgument,
                "The test-run operation input is invalid."));

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(TestRunErrorKind.InvalidInput, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.PrimaryFailure.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.PrimaryFailure.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenOneshotTransportIsInterruptedAfterResultsWereWritten_RecoversGeneratedArtifactVerdict ()
    {
        const Verdict RecoveredVerdict = Verdict.Incomplete;
        using var scope = TestDirectories.CreateTempScope(
            "test-run-service",
            "oneshot-stream-ended-after-results");
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(scope.GetPath("artifacts"));
        var convertCount = 0;

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, artifactPaths, _, _) =>
            {
                WriteGeneratedTestArtifacts(artifactPaths);
                return ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.IpcTransportInterrupted(
                    "Failed to execute Unity oneshot IPC request. Pipe is broken.",
                    TestRunErrorCodes.UnityTestExecutionFailed));
            }),
            resultsConverter: new StubUnityResultsConverter(convertSession =>
            {
                convertCount++;
                Assert.Equal(session, convertSession);
                return ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(RecoveredVerdict));
            }));

        var result = Assert.IsType<TestRunCompletedServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(RecoveredVerdict, result.Verdict);
        Assert.Equal(1, convertCount);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenGeneralOneshotFailureDescribesTransportInterruption_DoesNotRecover ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "test-run-service",
            "general-oneshot-transport-message");
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(scope.GetPath("artifacts"));
        var convertCount = 0;
        const string failureMessage =
            "Failed to execute Unity oneshot IPC request. Pipe is broken. Unity process cleanup did not complete.";

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, artifactPaths, _, _) =>
            {
                WriteGeneratedTestArtifacts(artifactPaths);
                return ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.InternalError(
                    failureMessage,
                    UcliCoreErrorCodes.InternalError));
            }),
            resultsConverter: new StubUnityResultsConverter(_ =>
            {
                convertCount++;
                return ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass));
            }));

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(UcliCoreErrorCodes.InternalError, result.PrimaryFailure.Code);
        Assert.Equal(failureMessage, result.Message);
        Assert.Equal(0, convertCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenOneshotTransportIsInterruptedBeforeResultsWereWritten_ReturnsInfrastructureFailure ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "test-run-service",
            "oneshot-stream-ended-before-results");
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(scope.GetPath("artifacts"));
        var convertCount = 0;

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, artifactPaths, _, _) =>
            {
                Directory.CreateDirectory(artifactPaths.ArtifactsDir.Value);
                File.WriteAllText(artifactPaths.EditorLogPath.Value, string.Empty);
                return ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.IpcTransportInterrupted(
                    "Failed to execute Unity oneshot IPC request. Pipe is broken.",
                    UcliCoreErrorCodes.InternalError));
            }),
            resultsConverter: new StubUnityResultsConverter(_ =>
            {
                convertCount++;
                return ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass));
            }));

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.PrimaryFailure.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.PrimaryFailure.Code);
        Assert.Equal(0, convertCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenOneshotTransportIsInterruptedWithInvalidResultsXml_ReturnsInfrastructureFailure ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "test-run-service",
            "oneshot-stream-ended-with-invalid-results");
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(scope.GetPath("artifacts"));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, artifactPaths, _, _) =>
            {
                Directory.CreateDirectory(artifactPaths.ArtifactsDir.Value);
                File.WriteAllText(artifactPaths.ResultsXmlPath.Value, "not xml");
                File.WriteAllText(artifactPaths.EditorLogPath.Value, string.Empty);
                return ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.IpcTransportInterrupted(
                    "Failed to execute Unity oneshot IPC request. Pipe is broken.",
                    UcliCoreErrorCodes.InternalError));
            }),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(
                TestRunResultTestValues.CreateConversionFailure(
                    UnityResultsConversionFailureKind.InvalidResultsXml,
                    "Unity results XML is invalid."))));

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationOutcome.ToolError, result.PrimaryFailure.Outcome);
        Assert.Equal(TestRunErrorCodes.TestResultsXmlInvalid, result.PrimaryFailure.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithCallerCancellationDuringUnityExecution_ReturnsCanceledToolErrorWithRunContext ()
    {
        using var scope = CreateArtifactsScope();
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(scope.GetPath("artifacts"));
        using var cancellationTokenSource = new CancellationTokenSource();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
            {
                cancellationTokenSource.Cancel();
                return ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.Canceled(
                    "Unity process execution was canceled.",
                    ExecutionErrorCodes.Canceled));
            }),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass))));

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(ApplicationOutcome.ToolError, result.PrimaryFailure.Outcome);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.PrimaryFailure.Code);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
    }

    private static void WriteGeneratedTestArtifacts (ArtifactPaths artifactPaths)
    {
        Directory.CreateDirectory(artifactPaths.ArtifactsDir.Value);
        File.WriteAllText(
            artifactPaths.ResultsXmlPath.Value,
            """
            <test-run>
              <test-case fullname="MackySoft.Ucli.Unity.Tests.Sample.Pass" result="Passed" duration="0.001" />
            </test-run>
            """);
        File.WriteAllText(artifactPaths.EditorLogPath.Value, string.Empty);
    }
}
