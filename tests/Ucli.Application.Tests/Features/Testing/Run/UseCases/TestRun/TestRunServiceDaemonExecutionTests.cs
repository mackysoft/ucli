using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Testing;
using static MackySoft.Ucli.Application.Tests.TestRunServiceTestFactory;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunServiceDaemonExecutionTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithDaemonTarget_UsesDaemonClient ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, artifactPaths, _, _, _) =>
            CompleteDaemonRequest(artifactPaths));
        var unityTestExecutor = new StubUnityTestExecutor((_, _, _, _) =>
            throw new InvalidOperationException("Oneshot test execution was not expected."));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: unityTestExecutor,
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass))),
            daemonTestRunClient: daemonTestRunClient);

        var result = Assert.IsType<TestRunCompletedServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(Verdict.Pass, result.Verdict);
        DaemonTestRunClientAssert.ExecutionRequested(daemonTestRunClient, expectedFailFast: false);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonLifecycleGateFails_ReturnsFailedRunWithLifecycleErrorCode ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    EditorLifecycleErrorCodes.EditorBusy,
                    "Unity editor is busy with internal work.",
                    startupFailure: null))));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass))),
            daemonTestRunClient: daemonTestRunClient);

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(
                CreateInput() with { FailFast = true },
                cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.PrimaryFailure.Outcome);
        Assert.Equal(EditorLifecycleErrorCodes.EditorBusy, result.PrimaryFailure.Code);
        DaemonTestRunClientAssert.ExecutionRequested(daemonTestRunClient, expectedFailFast: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonExecutionTimesOut_ReturnsIpcTimeoutErrorCode ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    ExecutionErrorCodes.IpcTimeout,
                    "Unity daemon test run request timed out.",
                    startupFailure: null))));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass))),
            daemonTestRunClient: daemonTestRunClient);

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.PrimaryFailure.Outcome);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.PrimaryFailure.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonResponseReportsIpcTimeout_ReturnsIpcTimeoutErrorCode ()
    {
        var configuration = CreateResolvedConfiguration();

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
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
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.PrimaryFailure.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonSessionDisappearsAfterModeResolution_PreservesDaemonNotRunningCode ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                    "Unity daemon is not running. Daemon session token is not available."))));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass))),
            daemonTestRunClient: daemonTestRunClient);

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationOutcome.ToolError, result.PrimaryFailure.Outcome);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.PrimaryFailure.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonSessionTokenResolutionFailsInternally_ReturnsFailedRun ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    UcliCoreErrorCodes.InternalError,
                    "Daemon session token could not be resolved. session store read failed"))));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass))),
            daemonTestRunClient: daemonTestRunClient);

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.PrimaryFailure.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.PrimaryFailure.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonSessionTokenResolutionReturnsInvalidArgument_ReturnsFailedRun ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    UcliCoreErrorCodes.InvalidArgument,
                    "Daemon session token could not be resolved. Daemon session token is missing.",
                    startupFailure: null))));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass))),
            daemonTestRunClient: daemonTestRunClient);

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.PrimaryFailure.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.PrimaryFailure.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenArtifactsCompletionFailsAfterDaemonTimeout_PreservesBothCommandErrors ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    ExecutionErrorCodes.IpcTimeout,
                    "Unity daemon test run request timed out.",
                    startupFailure: null))));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Failure(
                    ExecutionError.InternalError("completion failed", UcliCoreErrorCodes.InternalError))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass))),
            daemonTestRunClient: daemonTestRunClient);

        var result = Assert.IsType<TestRunAfterCreationCommandErrorWithFinalizationServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.PrimaryFailure.Outcome);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.PrimaryFailure.Code);
        Assert.Equal(ApplicationFailureKind.InternalError, result.Failures[1].Kind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Failures[1].Code);
        Assert.Equal("completion failed", result.Failures[1].Message);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonArtifactsAreMissing_ReturnsFailedRun ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "test-run-service",
            "daemon-artifacts-missing");
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(scope.GetPath("artifacts"));
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(CreateSuccessfulUnityRequestResult(0)));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass))),
            daemonTestRunClient: daemonTestRunClient);

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.PrimaryFailure.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionFailed, result.PrimaryFailure.Code);
        Assert.StartsWith(
            "Unity process completed but results.xml was not generated:",
            result.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenDaemonTransportIsInterrupted_DoesNotRecoverFromGeneratedArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "test-run-service",
            "daemon-transport-interrupted");
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(scope.GetPath("artifacts"));
        var convertCount = 0;
        const string failureMessage =
            "Failed to execute Unity daemon IPC request. Pipe is broken.";
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, artifactPaths, _, _, _) =>
        {
            Directory.CreateDirectory(artifactPaths.ArtifactsDir.Value);
            File.WriteAllText(artifactPaths.ResultsXmlPath.Value, "<test-run />");
            File.WriteAllText(artifactPaths.EditorLogPath.Value, string.Empty);
            return ValueTask.FromResult(UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.TransportInterrupted,
                    UcliCoreErrorCodes.InternalError,
                    failureMessage)));
        });

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult<UnityTestExecutionResult>(UnityTestExecutionResult.FromProcessExitCode(0))),
            resultsConverter: new StubUnityResultsConverter(_ =>
            {
                convertCount++;
                return ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass));
            }),
            daemonTestRunClient: daemonTestRunClient);

        var result = Assert.IsType<TestRunAfterCreationPrimaryCommandErrorServiceResult>(
            await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None));

        Assert.Equal(UcliCoreErrorCodes.InternalError, result.PrimaryFailure.Code);
        Assert.Equal(failureMessage, result.Message);
        Assert.Equal(0, convertCount);
    }

    private static ValueTask<UnityRequestExecutionResult> CompleteDaemonRequest (ArtifactPaths artifactPaths)
    {
        Directory.CreateDirectory(artifactPaths.ArtifactsDir.Value);
        File.WriteAllText(artifactPaths.ResultsXmlPath.Value, "<test-run />");
        File.WriteAllText(artifactPaths.EditorLogPath.Value, string.Empty);
        return ValueTask.FromResult(CreateSuccessfulUnityRequestResult(0));
    }

}
