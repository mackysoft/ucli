using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.TestRunServiceTestFactory;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunServiceDaemonExecutionTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithDaemonTarget_UsesDaemonClient ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Success(0)));
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
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Equal(TestRunResultKind.Pass, result.Result);
        Assert.Equal(ApplicationOutcome.Success, result.Outcome);
        DaemonTestRunClientAssert.ExecutionRequested(daemonTestRunClient, expectedFailFast: false);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonLifecycleGateFails_ReturnsInfraErrorWithLifecycleErrorCode ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.AbnormalExit,
                "Unity editor is busy with internal work.",
                EditorLifecycleErrorCodes.EditorBusy)));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.ExecuteAsync(CreateInput() with { FailFast = true }, cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(EditorLifecycleErrorCodes.EditorBusy, result.ErrorCode);
        DaemonTestRunClientAssert.ExecutionRequested(daemonTestRunClient, expectedFailFast: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonExecutionTimesOut_ReturnsIpcTimeoutErrorCode ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.IpcTimedOut,
                "Unity daemon test run request timed out.")));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
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
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: new RecordingDaemonTestRunClient((_, _, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            unityRequestResponse: CreateFailureUnityRequestResponse(
                IpcTransportErrorCodes.IpcTimeout,
                "Unity test run timed out after 30000 milliseconds."));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonSessionDisappearsAfterModeResolution_PreservesDaemonNotRunningCode ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.StartFailed,
                "Unity daemon is not running. Daemon session token is not available.",
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning)));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonSessionTokenResolutionFailsInternally_ReturnsInfraError ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ClientSetupFailed,
                "Daemon session token could not be resolved. session store read failed",
                UcliCoreErrorCodes.InternalError)));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonSessionTokenResolutionReturnsInvalidArgument_ReturnsInfraError ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ClientSetupFailed,
                "Daemon session token could not be resolved. Daemon session token is missing.",
                UcliCoreErrorCodes.InvalidArgument)));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenArtifactsCompletionFailsAfterDaemonTimeout_PreservesPrimaryTimeoutError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.IpcTimedOut,
                "Unity daemon test run request timed out.")));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Failure(ExecutionError.InternalError("completion failed"))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonArtifactsAreMissing_ReturnsUnityTestExecutionFailed ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ArtifactMissing,
                "Generated test artifacts are missing.")));

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession()),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionFailed, result.ErrorCode);
        Assert.Equal("Generated test artifacts are missing.", result.Message);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WhenDaemonFailureMatchesOneshotRecoveryMessage_DoesNotRecoverFromGeneratedArtifacts ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "test-run-service",
            "daemon-failure-matches-oneshot-recovery");
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(scope.GetPath("artifacts"));
        var convertCount = 0;
        const string failureMessage =
            "Failed to execute Unity oneshot IPC request. IPC stream ended before a complete frame was read.";
        var daemonTestRunClient = new RecordingDaemonTestRunClient((_, artifactPaths, _, _, _) =>
        {
            Directory.CreateDirectory(artifactPaths.ArtifactsDir);
            File.WriteAllText(artifactPaths.ResultsXmlPath, "<test-run />");
            File.WriteAllText(artifactPaths.EditorLogPath, string.Empty);
            return ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.AbnormalExit,
                failureMessage,
                UcliCoreErrorCodes.InternalError));
        });

        var service = CreateService(
            configurationResolver: new StubTestRunConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubUnityResultsConverter(_ =>
            {
                convertCount++;
                return ValueTask.FromResult(UnityResultsConversionResult.Success(hasFailedTests: false));
            }),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal(failureMessage, result.Message);
        Assert.Equal(0, convertCount);
    }
}
