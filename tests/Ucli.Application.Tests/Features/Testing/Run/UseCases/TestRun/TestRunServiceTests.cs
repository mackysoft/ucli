using System.Text.Json;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Execution;
using MackySoft.Ucli.Application.Features.Testing.Run.Progress;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Preflight;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Equal((TestRunResultKind)expectedResult, result.Result);
        Assert.Null(result.ErrorKind);
        Assert.Equal((ApplicationOutcome)expectedOutcome, result.Outcome);
        Assert.Equal(session.RunId, result.RunId);
        Assert.Equal(session.Paths.ArtifactsDir, result.ArtifactsDir);
        Assert.Equal(session.Paths.SummaryJsonPath, result.SummaryJsonPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithProgressSink_EmitsRunStartedAndForwardsUnityProgress ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);
        var progressSink = new CollectingProgressSink();
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
                    "pass",
                    42,
                    null,
                    null))),
            new UnityRequestProgressFrame(
                TestRunProgressEventNames.RunDiagnostic,
                IpcPayloadCodec.SerializeToElement(new TestRunDiagnosticEntry(
                    session.RunId,
                    "TEST_PROGRESS_STUB",
                    "stub progress",
                    "info"))),
        };

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            streamingProgressFrames: unityProgressFrames);

        var result = await service.ExecuteAsync(CreateInput(), progressSink, CancellationToken.None);

        Assert.Equal(TestRunResultKind.Pass, result.Result);
        Assert.Collection(
            progressSink.Entries,
            entry =>
            {
                Assert.Equal(TestRunProgressEventNames.RunStarted, entry.EventName);
                var payload = Assert.IsType<TestRunStartedEntry>(entry.Payload);
                Assert.Equal(session.RunId, payload.RunId);
                Assert.Equal("editmode", payload.TestPlatform);
            },
            entry =>
            {
                Assert.Equal(TestRunProgressEventNames.RunStarted, entry.EventName);
                var payload = Assert.IsType<TestRunStartedEntry>(entry.Payload);
                Assert.Equal(session.RunId, payload.RunId);
                Assert.Equal("MyGame.Tests", Assert.Single(payload.AssemblyNames));
            },
            entry =>
            {
                Assert.Equal(TestRunProgressEventNames.CaseStarted, entry.EventName);
                var payload = Assert.IsType<TestCaseStartedEntry>(entry.Payload);
                Assert.Equal(session.RunId, payload.RunId);
                Assert.Equal("test-id", payload.TestId);
                Assert.Equal("SmokeTest.Passes", payload.TestName);
            },
            entry =>
            {
                Assert.Equal(TestRunProgressEventNames.CaseFinished, entry.EventName);
                var payload = Assert.IsType<TestCaseFinishedEntry>(entry.Payload);
                Assert.Equal(session.RunId, payload.RunId);
                Assert.Equal("pass", payload.Result);
                Assert.Equal(42, payload.DurationMilliseconds);
            },
            entry =>
            {
                Assert.Equal(TestRunProgressEventNames.RunDiagnostic, entry.EventName);
                var payload = Assert.IsType<TestRunDiagnosticEntry>(entry.Payload);
                Assert.Equal(session.RunId, payload.RunId);
                Assert.Equal("TEST_PROGRESS_STUB", payload.Code);
            });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnsupportedUnityProgressEvent_ReturnsToolError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);
        var progressSink = new CollectingProgressSink();

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            streamingProgressFrame: new UnityRequestProgressFrame(
                "test.run.unsupported",
                IpcPayloadCodec.SerializeToElement(new { runId = session.RunId })));

        var result = await service.ExecuteAsync(CreateInput(), progressSink, CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionFailed, result.ErrorCode);
        Assert.Contains("Unity test-run progress event is not supported", result.Message, StringComparison.Ordinal);
        Assert.Single(progressSink.Entries);
        Assert.Equal(TestRunProgressEventNames.RunStarted, progressSink.Entries[0].EventName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMismatchedUnityProgressRunId_ReturnsToolError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);
        var progressSink = new CollectingProgressSink();

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            streamingProgressFrame: new UnityRequestProgressFrame(
                TestRunProgressEventNames.CaseStarted,
                IpcPayloadCodec.SerializeToElement(new TestCaseStartedEntry(
                    "other-run-id",
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
        Assert.Single(progressSink.Entries);
        Assert.Equal(TestRunProgressEventNames.RunStarted, progressSink.Entries[0].EventName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithInvalidUnityProgressPayload_ReturnsToolError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);
        var progressSink = new CollectingProgressSink();

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            streamingProgressFrame: new UnityRequestProgressFrame(
                TestRunProgressEventNames.CaseFinished,
                JsonDocument.Parse("[]").RootElement.Clone()));

        var result = await service.ExecuteAsync(CreateInput(), progressSink, CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionFailed, result.ErrorCode);
        Assert.Contains("progress payload is invalid", result.Message, StringComparison.Ordinal);
        Assert.Single(progressSink.Entries);
        Assert.Equal(TestRunProgressEventNames.RunStarted, progressSink.Entries[0].EventName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithContractInvalidUnityProgressPayload_ReturnsToolError ()
    {
        var configuration = CreateResolvedConfiguration();
        var session = CreateArtifactsSession(configuration);
        var progressSink = new CollectingProgressSink();

        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            streamingProgressFrame: new UnityRequestProgressFrame(
                TestRunProgressEventNames.CaseFinished,
                IpcPayloadCodec.SerializeToElement(new TestCaseFinishedEntry(
                    session.RunId,
                    "test-id",
                    "SmokeTest.Passes",
                    "MyGame.Tests",
                    "editmode",
                    ["smoke"],
                    "unknown",
                    42,
                    null,
                    null))));

        var result = await service.ExecuteAsync(CreateInput(), progressSink, CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.ToolError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(TestRunErrorCodes.UnityTestExecutionFailed, result.ErrorCode);
        Assert.Contains("progress payload violates contract", result.Message, StringComparison.Ordinal);
        Assert.Contains("result", result.Message, StringComparison.Ordinal);
        Assert.Single(progressSink.Entries);
        Assert.Equal(TestRunProgressEventNames.RunStarted, progressSink.Entries[0].EventName);
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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InvalidInput, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithConfigDiagnostics_ReturnsInvalidInput ()
    {
        var configuration = CreateResolvedConfiguration();
        var service = CreateService(
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            configStore: new StubConfigStore(UcliConfigLoadResult.Failure(
            [
                UcliConfigDiagnostic.Create(
                    "config.semantic.unsupportedLiteral",
                    "operationPolicy",
                    "config.json",
                    "Config operationPolicy is invalid: unsupported."),
                UcliConfigDiagnostic.Create(
                    "config.semantic.unsupportedLiteral",
                    "planTokenMode",
                    "config.json",
                    "Config planTokenMode is invalid: never."),
            ])),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Oneshot, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => throw new InvalidOperationException(),
                complete: (_, _) => throw new InvalidOperationException()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) => ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))));

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InvalidInput, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Contains("operationPolicy", result.Message, StringComparison.Ordinal);
        Assert.Contains("planTokenMode", result.Message, StringComparison.Ordinal);
    }

    public static TheoryData<UcliCode, string> ModeContractErrorCases => new()
    {
        { UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, "Daemon is not running for mode=daemon." },
        { UnityExecutionModeDecisionErrorCodes.DaemonRunningOneshotForbidden, "Daemon is running for mode=oneshot." },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(ModeContractErrorCases))]
    public async Task Execute_WithModeContractError_ReturnsToolErrorWithModeCode (
        UcliCode errorCode,
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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Equal(TestRunResultKind.Pass, result.Result);
        Assert.Equal(ApplicationOutcome.Success, result.Outcome);
        Assert.Equal(1, daemonTestRunClient.CallCount);
        Assert.False(daemonTestRunClient.LastFailFast);
        Assert.Equal(0, unityTestExecutor.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenDaemonLifecycleGateFails_ReturnsInfraErrorWithLifecycleErrorCode ()
    {
        var configuration = CreateResolvedConfiguration();
        var daemonTestRunClient = new StubDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.AbnormalExit,
                "Unity editor is busy with internal work.",
                EditorLifecycleErrorCodes.EditorBusy)));

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

        var result = await service.ExecuteAsync(CreateInput() with { FailFast = true }, cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(EditorLifecycleErrorCodes.EditorBusy, result.ErrorCode);
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
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, true, UnityExecutionTarget.Daemon, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession(configuration)),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
            daemonTestRunClient: new StubDaemonTestRunClient((_, _, _, _, _) =>
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
        var daemonTestRunClient = new StubDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ClientSetupFailed,
                "Daemon session token could not be resolved. session store read failed",
                UcliCoreErrorCodes.InternalError)));

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
        var daemonTestRunClient = new StubDaemonTestRunClient((_, _, _, _, _) =>
            ValueTask.FromResult(UnityTestExecutionResult.Failure(
                UnityTestExecutionFailureKind.ClientSetupFailed,
                "Daemon session token could not be resolved. Daemon session token is missing.",
                UcliCoreErrorCodes.InvalidArgument)));

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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
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
            configurationResolver: new StubConfigurationResolver(TestRunConfigurationResolutionResult.Success(configuration)),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(UnityExecutionMode.Auto, false, UnityExecutionTarget.Oneshot, TimeSpan.FromSeconds(30)))),
            artifactsService: new StubArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(CreateArtifactsSession(configuration)),
                complete: (_, _) => ArtifactsCompletionResult.Success()),
            unityTestExecutor: new StubUnityTestExecutor((_, _, _, _) =>
                ValueTask.FromResult(UnityTestExecutionResult.Success(0))),
            resultsConverter: new StubResultsConverter(_ => ValueTask.FromResult(UnityResultsConversionResult.Success(false))),
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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: CancellationToken.None);

        Assert.Null(result.Result);
        Assert.Equal(TestRunErrorKind.InfraError, result.ErrorKind);
        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.ErrorCode);
        Assert.Equal("Unexpected error during Unity results conversion: boom", result.Message);
        Assert.Equal(session.RunId, result.RunId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunProgressPayloadValidator_WithMismatchedEventAndPayloadType_ThrowsProtocolViolation ()
    {
        var exception = Assert.Throws<TestRunProgressProtocolException>(() =>
            TestRunProgressPayloadValidator.Validate(
                TestRunProgressEventNames.CaseStarted,
                new TestRunDiagnosticEntry(
                    "run-id",
                    "TEST_PROGRESS_STUB",
                    "stub progress",
                    "info"),
                "run-id"));

        Assert.Contains("payload type violates contract", exception.Message, StringComparison.Ordinal);
        Assert.Contains(TestRunProgressEventNames.CaseStarted, exception.Message, StringComparison.Ordinal);
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

        var result = await service.ExecuteAsync(CreateInput(), cancellationToken: cancellationTokenSource.Token);

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
        StubUnityTestExecutor unityTestExecutor,
        IUnityResultsConverter resultsConverter,
        IUcliConfigStore? configStore = null,
        StubDaemonTestRunClient? daemonTestRunClient = null,
        UnityRequestProgressFrame? streamingProgressFrame = null,
        IReadOnlyList<UnityRequestProgressFrame>? streamingProgressFrames = null,
        UnityRequestResponse? unityRequestResponse = null)
    {
        var preflightService = new TestRunPreflightService(
            configurationResolver,
            configStore ?? new StubConfigStore(),
            modeDecisionService);
        var unityRequestExecutor = new StubUnityRequestExecutor(
            unityTestExecutor,
            daemonTestRunClient,
            streamingProgressFrames ?? (streamingProgressFrame is null ? null : [streamingProgressFrame]),
            unityRequestResponse);
        var executionPipeline = new TestRunExecutionPipeline(
            artifactsService,
            unityRequestExecutor,
            resultsConverter,
            new StubArtifactExistenceProbe(),
            unityRequestExecutor);
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

    private static UnityRequestResponse CreateFailureUnityRequestResponse (
        UcliCode code,
        string message)
    {
        return new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new { }),
            Errors:
            [
                new OperationExecutionError(code, message, OpId: null),
            ],
            HasFailureStatus: true,
            FailureStatus: IpcProtocol.StatusError);
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
        private readonly UcliConfigLoadResult loadResult;

        public StubConfigStore ()
            : this(UcliConfigLoadResult.Success(UcliConfig.CreateDefault(), ConfigSource.Default))
        {
        }

        public StubConfigStore (UcliConfigLoadResult loadResult)
        {
            this.loadResult = loadResult;
        }

        public string GetConfigPath (string storageRoot)
        {
            return Path.Combine(storageRoot, ".ucli", "config.json");
        }

        public ValueTask<UcliConfigLoadResult> LoadAsync (
            string storageRoot,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(loadResult);
        }

        public ValueTask<UcliConfigSaveResult> SaveAsync (
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

        public ValueTask<UnityExecutionModeDecisionResult> DecideAsync (
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

        public ValueTask<ArtifactsPreparationResult> PrepareAsync (
            ResolvedTestRunConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(prepare(configuration));
        }

        public ValueTask<ArtifactsCompletionResult> CompleteAsync (
            ResolvedTestRunConfiguration configuration,
            ArtifactsSession session,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(complete(configuration, session));
        }
    }

    private sealed class StubUnityTestExecutor
    {
        private readonly Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, CancellationToken, ValueTask<UnityTestExecutionResult>> execute;

        public StubUnityTestExecutor (Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, CancellationToken, ValueTask<UnityTestExecutionResult>> execute)
        {
            this.execute = execute;
        }

        public int CallCount { get; private set; }

        public ValueTask<UnityTestExecutionResult> ExecuteAsync (
            ResolvedTestRunConfiguration configuration,
            ArtifactPaths artifactPaths,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return execute(configuration, artifactPaths, timeout, cancellationToken);
        }
    }

    private sealed class StubDaemonTestRunClient
    {
        private readonly Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, bool, CancellationToken, ValueTask<UnityTestExecutionResult>> execute;

        public StubDaemonTestRunClient (Func<ResolvedTestRunConfiguration, ArtifactPaths, TimeSpan, bool, CancellationToken, ValueTask<UnityTestExecutionResult>> execute)
        {
            this.execute = execute;
        }

        public int CallCount { get; private set; }

        public bool LastFailFast { get; private set; }

        public ValueTask<UnityTestExecutionResult> ExecuteAsync (
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

    private sealed class StubUnityRequestExecutor : IUnityRequestExecutor, IUnityStreamingRequestExecutor
    {
        private readonly StubUnityTestExecutor unityTestExecutor;
        private readonly StubDaemonTestRunClient? daemonTestRunClient;
        private readonly IReadOnlyList<UnityRequestProgressFrame>? streamingProgressFrames;
        private readonly UnityRequestResponse? responseOverride;

        public StubUnityRequestExecutor (
            StubUnityTestExecutor unityTestExecutor,
            StubDaemonTestRunClient? daemonTestRunClient,
            IReadOnlyList<UnityRequestProgressFrame>? streamingProgressFrames,
            UnityRequestResponse? responseOverride)
        {
            this.unityTestExecutor = unityTestExecutor;
            this.daemonTestRunClient = daemonTestRunClient;
            this.streamingProgressFrames = streamingProgressFrames;
            this.responseOverride = responseOverride;
        }

        public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            CancellationToken cancellationToken = default)
        {
            return ExecuteCoreAsync(timeout, payload, onProgressFrame: null, cancellationToken);
        }

        public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            Func<UnityRequestProgressFrame, CancellationToken, ValueTask> onProgressFrame,
            CancellationToken cancellationToken = default)
        {
            return ExecuteCoreAsync(timeout, payload, onProgressFrame, cancellationToken);
        }

        private async ValueTask<UnityRequestExecutionResult> ExecuteCoreAsync (
            TimeSpan timeout,
            UnityRequestPayload payload,
            Func<UnityRequestProgressFrame, CancellationToken, ValueTask>? onProgressFrame,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (responseOverride is not null)
            {
                return UnityRequestExecutionResult.Success(responseOverride);
            }

            var testRunRequest = ReadTestRunRequest(payload);
            var artifactPaths = CreateArtifactPaths(testRunRequest);
            var configuration = CreateResolvedConfiguration();
            var executionResult = daemonTestRunClient is null
                ? await unityTestExecutor.ExecuteAsync(configuration, artifactPaths, timeout, cancellationToken)
                    .ConfigureAwait(false)
                : await daemonTestRunClient.ExecuteAsync(configuration, artifactPaths, timeout, testRunRequest.FailFast, cancellationToken)
                    .ConfigureAwait(false);

            if (executionResult.IsSuccess)
            {
                EnsureArtifactFiles(artifactPaths);
                if (onProgressFrame is not null)
                {
                    var progressFrames = streamingProgressFrames ?? [
                        new UnityRequestProgressFrame(
                            TestRunProgressEventNames.RunDiagnostic,
                            IpcPayloadCodec.SerializeToElement(new TestRunDiagnosticEntry(
                                testRunRequest.RunId ?? "run-id",
                                "TEST_PROGRESS_STUB",
                                "stub progress",
                                "info"))),
                    ];
                    foreach (var progressFrame in progressFrames)
                    {
                        await onProgressFrame(progressFrame, cancellationToken).ConfigureAwait(false);
                    }
                }

                return UnityRequestExecutionResult.Success(new UnityRequestResponse(
                    IpcPayloadCodec.SerializeToElement(new IpcTestRunResponse(executionResult.ProcessExitCode!.Value)),
                    Array.Empty<OperationExecutionError>(),
                    HasFailureStatus: false));
            }

            return UnityRequestExecutionResult.Failure(new UnityRequestFailure(
                ResolveErrorCode(executionResult),
                executionResult.ErrorMessage ?? "Unity test execution failed.",
                executionResult.StartupFailure));
        }

        private static UnityRequestPayload.TestRun ReadTestRunRequest (UnityRequestPayload payload)
        {
            return Assert.IsType<UnityRequestPayload.TestRun>(payload);
        }

        private static ArtifactPaths CreateArtifactPaths (UnityRequestPayload.TestRun request)
        {
            var artifactsDir = Path.GetDirectoryName(request.ResultsXmlPath) ?? Path.GetTempPath();
            return new ArtifactPaths(
                ArtifactsDir: artifactsDir,
                MetaJsonPath: Path.Combine(artifactsDir, "meta.json"),
                ResultsXmlPath: request.ResultsXmlPath,
                EditorLogPath: request.EditorLogPath,
                ResultsJsonPath: Path.Combine(artifactsDir, "results.json"),
                SummaryJsonPath: Path.Combine(artifactsDir, "summary.json"));
        }

        private static void EnsureArtifactFiles (ArtifactPaths artifactPaths)
        {
            Directory.CreateDirectory(artifactPaths.ArtifactsDir);
            File.WriteAllText(artifactPaths.ResultsXmlPath, "<test-run />");
            File.WriteAllText(artifactPaths.EditorLogPath, string.Empty);
        }

        private static UcliCode ResolveErrorCode (UnityTestExecutionResult executionResult)
        {
            if (executionResult.ErrorCode is { IsValid: true } code)
            {
                return code;
            }

            return executionResult.FailureKind switch
            {
                UnityTestExecutionFailureKind.IpcTimedOut => ExecutionErrorCodes.IpcTimeout,
                UnityTestExecutionFailureKind.ProcessTimedOut => ExecutionErrorCodes.IpcTimeout,
                UnityTestExecutionFailureKind.Canceled => ExecutionErrorCodes.Canceled,
                UnityTestExecutionFailureKind.ArtifactMissing => TestRunErrorCodes.UnityTestExecutionFailed,
                _ => UcliCoreErrorCodes.InternalError,
            };
        }
    }

    private sealed class CollectingProgressSink : ITestRunProgressSink
    {
        private readonly List<ProgressEntry> entries = [];

        public IReadOnlyList<ProgressEntry> Entries => entries;

        public ValueTask OnEntryAsync (
            string eventName,
            object payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new ProgressEntry(eventName, payload));
            return ValueTask.CompletedTask;
        }
    }

    private sealed record ProgressEntry (
        string EventName,
        object Payload);

    private sealed class StubArtifactExistenceProbe : ITestRunArtifactExistenceProbe
    {
        public TestRunArtifactExistenceResult ValidateGeneratedFiles (ArtifactPaths artifactPaths)
        {
            if (!File.Exists(artifactPaths.ResultsXmlPath))
            {
                return TestRunArtifactExistenceResult.Failure(
                    $"Unity process completed but results.xml was not generated: {artifactPaths.ResultsXmlPath}");
            }

            if (!File.Exists(artifactPaths.EditorLogPath))
            {
                return TestRunArtifactExistenceResult.Failure(
                    $"Unity process completed but editor.log was not generated: {artifactPaths.EditorLogPath}");
            }

            return TestRunArtifactExistenceResult.Success();
        }
    }

    private sealed class StubResultsConverter : IUnityResultsConverter
    {
        private readonly Func<ArtifactsSession, ValueTask<UnityResultsConversionResult>> convert;

        public StubResultsConverter (Func<ArtifactsSession, ValueTask<UnityResultsConversionResult>> convert)
        {
            this.convert = convert;
        }

        public ValueTask<UnityResultsConversionResult> ConvertAsync (
            ArtifactsSession session,
            CancellationToken cancellationToken = default)
        {
            return convert(session);
        }
    }
}
