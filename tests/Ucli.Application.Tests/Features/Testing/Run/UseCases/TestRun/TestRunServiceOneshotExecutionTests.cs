using MackySoft.Tests;
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
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.ProcessTimedOut,
                    "Unity process timed out after 30000 milliseconds."))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionTimeout, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenOneshotResponseReportsIpcTimeout_ReturnsExecutionTimeoutErrorCode ()
    {
        var configuration = CreateResolvedConfiguration();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            unityRequestResponse: CreateFailureUnityRequestResponse(
                IpcTransportErrorCodes.IpcTimeout,
                "Unity test run timed out after 30000 milliseconds."));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionTimeout, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenOneshotIpcStreamEndsAfterResultsWereWritten_RecoversFromGeneratedArtifacts ()
    {
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
                return ValueTask.FromResult(UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.AbnormalExit,
                    "Failed to execute Unity oneshot IPC request. IPC stream ended before a complete frame was read.",
                    UcliCoreErrorCodes.InternalError));
            }),
            resultsConverter: new StubUnityResultsConverter(convertSession =>
            {
                convertCount++;
                Assert.Same(session, convertSession);
                return ValueTask.FromResult(UnityResultsConversionResult.Success(hasFailedTests: false, reportedTestCaseCount: 799));
            }));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Equal(TestRunResultKind.Pass, result.Result);
        Assert.Null(result.ErrorKind);
        Assert.Equal(ApplicationOutcome.Success, result.Outcome);
        Assert.Equal(1, convertCount);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenOneshotIpcStreamEndsBeforeResultsWereWritten_DoesNotRecoverFromEditorLogPlaceholder ()
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
                Directory.CreateDirectory(artifactPaths.ArtifactsDir);
                File.WriteAllText(artifactPaths.EditorLogPath, string.Empty);
                return ValueTask.FromResult(UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.AbnormalExit,
                    "Failed to execute Unity oneshot IPC request. IPC stream ended before a complete frame was read.",
                    UcliCoreErrorCodes.InternalError));
            }),
            resultsConverter: new StubUnityResultsConverter(_ =>
            {
                convertCount++;
                return ValueTask.FromResult(UnityResultsConversionResult.Success(hasFailedTests: false));
            }));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.StartsWith("Failed to execute Unity oneshot IPC request.", result.Message, StringComparison.Ordinal);
        Assert.Equal(0, convertCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenOneshotIpcStreamEndsWithInvalidResultsXml_DoesNotReportPass ()
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
                Directory.CreateDirectory(artifactPaths.ArtifactsDir);
                File.WriteAllText(artifactPaths.ResultsXmlPath, "not xml");
                File.WriteAllText(artifactPaths.EditorLogPath, string.Empty);
                return ValueTask.FromResult(UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.AbnormalExit,
                    "Failed to execute Unity oneshot IPC request. IPC stream ended before a complete frame was read.",
                    UcliCoreErrorCodes.InternalError));
            }),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(
                UnityResultsConversionResult.Failure(
                    UnityResultsConversionFailureKind.InvalidResultsXml,
                    "Unity results XML is invalid."))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithCallerCancellationDuringUnityExecution_ReturnsCanceledToolErrorWithRunContext ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();
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
                return ValueTask.FromResult(UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.Canceled,
                    "Unity process execution was canceled."));
            }),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: cancellationTokenSource.Token);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    private static void WriteGeneratedTestArtifacts (ArtifactPaths artifactPaths)
    {
        Directory.CreateDirectory(artifactPaths.ArtifactsDir);
        File.WriteAllText(
            artifactPaths.ResultsXmlPath,
            """
            <test-run>
              <test-case fullname="MackySoft.Ucli.Unity.Tests.Sample.Pass" result="Passed" duration="0.001" />
            </test-run>
            """);
        File.WriteAllText(artifactPaths.EditorLogPath, string.Empty);
    }
}
