using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Preflight;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunServiceTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false, (int)TestRunResultKind.Pass, (int)ApplicationOutcome.Success)]
    [InlineData(true, (int)TestRunResultKind.Fail, (int)ApplicationOutcome.TestFailure)]
    public async Task Execute_WithSuccessfulExecution_ReturnsPassOrFail (
        bool hasFailedTests,
        int expectedResult,
        int expectedOutcome)
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(hasFailedTests))));

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Equal((TestRunResultKind)expectedResult, result.Result);
        Assert.Null(result.ErrorKind);
        Assert.Equal((ApplicationOutcome)expectedOutcome, result.Outcome);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithConfigurationInvalidArgumentFailure_ReturnsInvalidInput ()
    {
        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(
                TestRunConfigurationResolutionResult.Failure(
                [
                    ExecutionError.InvalidArgument("testPlatform must be editmode, playmode, or a Unity BuildTarget literal."),
                ])),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => throw new InvalidOperationException(),
                complete: (_, _) => throw new InvalidOperationException()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))));

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InvalidInput, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, "Daemon is not running for mode=daemon.")]
    [InlineData(UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden, "Daemon is running for mode=oneshot.")]
    public async Task Execute_WithModeContractError_ReturnsToolErrorWithModeCode (
        string errorCode,
        string message)
    {
        var configuration = CreateResolvedConfiguration();

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.ContractFailure(
                new UnityExecutionModeDecisionContractError(
                    errorCode,
                    message))),
            artifactsService: new StubArtifactsService(
                prepare: _ => throw new InvalidOperationException(),
                complete: (_, _) => throw new InvalidOperationException()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))));

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithDaemonTarget_UsesDaemonClient ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new StubDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Success(0)));
        var unityTestExecutor = new StubUnityTestExecutor((_, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.StartFailed,
                "oneshot should not be executed.")));

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession(configuration)),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: unityTestExecutor,
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Equal(TestRunResultKind.Pass, result.Result);
        Assert.Equal(ApplicationOutcome.Success, result.Outcome);
        Assert.Equal(1, daemonTestRunClient.CallCount);
        Assert.False(daemonTestRunClient.LastFailFast);
        Assert.Equal(0, unityTestExecutor.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonLifecycleGateFails_PreservesLifecycleErrorCode ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new StubDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.AbnormalExit,
                "Unity editor is busy with internal work.",
                IpcErrorCodes.EditorBusy)));

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession(configuration)),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.Execute(CreateInput() with { FailFast = true }, CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(IpcErrorCodes.EditorBusy, result.ErrorCode);
        Assert.Equal(1, daemonTestRunClient.CallCount);
        Assert.True(daemonTestRunClient.LastFailFast);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonExecutionTimesOut_ReturnsIpcTimeoutErrorCode ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new StubDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.IpcTimedOut,
                "Unity daemon test run request timed out.")));

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession(configuration)),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonSessionDisappearsAfterModeResolution_PreservesDaemonNotRunningCode ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new StubDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.StartFailed,
                "Unity daemon is not running. Daemon session token is not available.",
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning)));

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession(configuration)),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.Execute(CreateInput(), CancellationToken.None);

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
        var daemonTestRunClient = new StubDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ClientSetupFailed,
                "Daemon session token could not be resolved. session store read failed",
                IpcErrorCodes.InternalError)));

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession(configuration)),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonSessionTokenResolutionReturnsInvalidArgument_ReturnsInfraError ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new StubDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ClientSetupFailed,
                "Daemon session token could not be resolved. Daemon session token is missing.",
                IpcErrorCodes.InvalidArgument)));

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession(configuration)),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAutoModeFallsBackToOneshotAndExecutionTimesOut_ReturnsExecutionTimeoutErrorCode ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.ProcessTimedOut,
                    "Unity process timed out after 30000 milliseconds."))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))));

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionTimeout, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenArtifactsCompletionFailsAfterDaemonTimeout_PreservesPrimaryTimeoutError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);
        var daemonTestRunClient = new StubDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.IpcTimedOut,
                "Unity daemon test run request timed out.")));

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Failure(ExecutionError.InternalError("completion failed"))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenArtifactsCompletionFailsAfterConversionFailure_PreservesPrimaryConversionError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Failure(ExecutionError.InternalError("completion failed"))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.ResultsXmlReadFailed,
                "Failed to read results.xml."))));

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.TestResultsXmlReadFailed, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenArtifactsCompletionFailsAfterFailedTests_PreservesFailResult ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Failure(ExecutionError.InternalError("completion failed"))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(hasFailedTests: true))));

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Equal(TestRunResultKind.Fail, result.Result);
        Assert.Null(result.ErrorKind);
        Assert.Equal(ApplicationOutcome.TestFailure, result.Outcome);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonArtifactsAreMissing_ReturnsUnityTestExecutionFailed ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new StubDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ArtifactMissing,
                "Generated test artifacts are missing.")));

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession(configuration)),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: daemonTestRunClient);

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionFailed, result.ErrorCode);
        Assert.Equal("Generated test artifacts are missing.", result.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithConversionOutputWriteFailure_ReturnsInfraError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.OutputWriteFailed,
                "Failed to write results artifacts."))));

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.TestResultsOutputWriteFailed, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithConversionResultsXmlReadFailure_ReturnsInfraError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Failure(
                UnityResultsConversionFailureKind.ResultsXmlReadFailed,
                "Failed to read results.xml."))));

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.TestResultsXmlReadFailed, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnexpectedConversionException_ReturnsInfraError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => throw new InvalidOperationException("boom")));

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnexpectedConversionExceptionAndCompletionFailure_PreservesConversionError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Failure(ExecutionError.InternalError("completion failed"))),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => throw new InvalidOperationException("boom")));

        var result = await service.Execute(CreateInput(), CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(IpcErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal("Unexpected error during Unity results conversion: boom", result.Message);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithCallerCancellationDuringUnityExecution_ReturnsCanceledToolErrorWithRunContext ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);
        using var cancellationTokenSource = new CancellationTokenSource();

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
            {
                cancellationTokenSource.Cancel();
                return ValueTask.FromResult(UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.Canceled,
                    "Unity process execution was canceled."));
            }),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))));

        var result = await service.Execute(CreateInput(), cancellationTokenSource.Token);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.ErrorCode);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    private static TestRunService CreateService (
        ITestRunConfigurationResolver configurationResolver,
        IUnityExecutionModeDecisionService modeDecisionService,
        ITestRunArtifactsService artifactsService,
        IUnityTestExecutor unityTestExecutor,
        IUnityResultsConverter resultsConverter,
        IDaemonTestRunClient? daemonTestRunClient = null)
    {
        var preflightService = new TestRunPreflightService(
            configurationResolver,
            new StubConfigStore(),
            modeDecisionService);
        var executionPipeline = new TestRunExecutionPipeline(
            artifactsService,
            unityTestExecutor,
            daemonTestRunClient ?? new StubDaemonTestRunClient((_, _, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Failure(
                    UnityTestExecutionFailureKind.StartFailed,
                    "Daemon test run client was not configured."))),
            resultsConverter);
        var resultMapper = new TestRunResultMapper();

        return new TestRunService(
            preflightService,
            executionPipeline,
            resultMapper);
    }

    private static TestRunCommandInput CreateInput ()
    {
        return new TestRunCommandInput(
            ProjectPath: null,
            ProfilePath: null,
            Mode: NormalizeMode(null),
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: NormalizeTestPlatform(null),
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutMilliseconds: null);
    }

    private static ResolvedTestRunConfiguration CreateResolvedConfiguration (UnityExecutionMode mode = UnityExecutionMode.Auto)
    {
        var projectPath = Path.GetFullPath("./sandbox/Unity");
        return new ResolvedTestRunConfiguration(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: projectPath,
                RepositoryRoot: projectPath,
                ProjectFingerprint: "fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            Mode: mode,
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: Path.GetFullPath("./Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategories: [],
            AssemblyNames: [],
            TestSettingsPath: null,
            TimeoutMilliseconds: null);
    }

    private static ArtifactsSession CreateArtifactsSession (ResolvedTestRunConfiguration configuration)
    {
        var artifactsDir = Path.Combine(Path.GetTempPath(), "ucli-test-run", "run-id");
        return new ArtifactsSession(
            RunId: "run-id",
            Paths: TestArtifactPaths.Create(artifactsDir),
            StartedAtUtc: DateTimeOffset.UtcNow);
    }

    private sealed class StubConfigurationResolver : ITestRunConfigurationResolver
    {
        private readonly TestRunConfigurationResolutionResult result;

        public StubConfigurationResolver (TestRunConfigurationResolutionResult result)
        {
            this.result = result;
        }

        public ValueTask<TestRunConfigurationResolutionResult> ResolveAsync (
            TestRunConfigurationRequest input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubConfigStore : IUcliConfigStore
    {
        public string GetConfigPath (string storageRoot)
        {
            return Path.Combine(storageRoot, ".ucli", "config.json");
        }

        public ValueTask<UcliConfigLoadResult> Load (
            string storageRoot,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(UcliConfigLoadResult.Success(UcliConfig.CreateDefault(), ConfigSource.Default));
        }

        public ValueTask<UcliConfigSaveResult> Save (
            string storageRoot,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(UcliConfigSaveResult.Success());
        }
    }

    private sealed class StubModeDecisionService : IUnityExecutionModeDecisionService
    {
        private readonly UnityExecutionModeDecisionResult result;

        public StubModeDecisionService (UnityExecutionModeDecisionResult result)
        {
            this.result = result;
        }

        public ValueTask<UnityExecutionModeDecisionResult> Decide (
            UnityExecutionMode mode,
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubArtifactsService : ITestRunArtifactsService
    {
        private readonly Func<ResolvedTestRunConfiguration, ArtifactsPreparationResult> prepare;

        private readonly Func<ResolvedTestRunConfiguration, ArtifactsSession, ArtifactsCompletionResult> complete;

        public StubArtifactsService (
            Func<ResolvedTestRunConfiguration, ArtifactsPreparationResult> prepare,
            Func<ResolvedTestRunConfiguration, ArtifactsSession, ArtifactsCompletionResult> complete)
        {
            this.prepare = prepare;
            this.complete = complete;
        }

        public ValueTask<ArtifactsPreparationResult> Prepare (
            ResolvedTestRunConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(prepare(configuration));
        }

        public ValueTask<ArtifactsCompletionResult> Complete (
            ResolvedTestRunConfiguration configuration,
            ArtifactsSession session,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(complete(configuration, session));
        }
    }

    private sealed class StubUnityTestExecutor : IUnityTestExecutor
    {
        private readonly Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, CancellationToken, ValueTask<UnityTestExecutionResult>> execute;

        public StubUnityTestExecutor (Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, CancellationToken, ValueTask<UnityTestExecutionResult>> execute)
        {
            this.execute = execute;
        }

        public int CallCount { get; private set; }

        public ValueTask<UnityTestExecutionResult> Execute (
            ResolvedTestRunConfiguration configuration,
            ArtifactPaths artifactPaths,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return execute(configuration, artifactPaths, timeout, cancellationToken);
        }
    }

    private sealed class StubDaemonTestRunClient : IDaemonTestRunClient
    {
        private readonly Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, bool, CancellationToken, ValueTask<UnityTestExecutionResult>> execute;

        public StubDaemonTestRunClient (Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, bool, CancellationToken, ValueTask<UnityTestExecutionResult>> execute)
        {
            this.execute = execute;
        }

        public int CallCount { get; private set; }

        public bool LastFailFast { get; private set; }

        public ValueTask<UnityTestExecutionResult> Execute (
            ResolvedTestRunConfiguration configuration,
            ArtifactPaths artifactPaths,
            TimeSpan timeout,
            bool failFast,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastFailFast = failFast;
            return execute(configuration, artifactPaths, timeout, failFast, cancellationToken);
        }
    }

    private sealed class StubResultsConverter : IUnityResultsConverter
    {
        private readonly Func<ArtifactsSession, ValueTask<UnityResultsConversionResult>> convert;

        public StubResultsConverter (Func<ArtifactsSession, ValueTask<UnityResultsConversionResult>> convert)
        {
            this.convert = convert;
        }

        public ValueTask<UnityResultsConversionResult> Convert (
            ArtifactsSession session,
            CancellationToken cancellationToken = default)
        {
            return convert(session);
        }
    }
}
