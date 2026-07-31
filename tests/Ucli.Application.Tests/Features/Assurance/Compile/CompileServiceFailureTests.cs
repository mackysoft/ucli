using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

public sealed class CompileServiceFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenRegisteredHostExitIsConfirmed_PublishesUnityExitedTerminalBeforeReturning ()
    {
        var observedAtUtc = StartedAtUtc.AddSeconds(1);
        var start = CreateStart();
        var terminalReference = CreateFailedTerminalReference();
        var terminalRecord = new CompileLifecycleExecutionTerminalRecord(
            ExecutionId,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            terminalGeneration: null,
            start.DeadlineUtc,
            start.StartedAtUtc,
            observedAtUtc,
            LifecycleExecutionTerminalReason.UnityExited,
            ExecutionApplicationState.NotApplied,
            result: null,
            verdict: null,
            Array.Empty<ArtifactRef>());
        var terminalizer = new RecordingLifecycleExecutionHostExitTerminalizer(
            new LifecycleExecutionHostExitTerminalizationResult.Published(
                terminalReference,
                terminalRecord));
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(
                UnityRequestExecutionResult.Failure(
                    new UnityRequestFailure(
                        UnityRequestFailureKind.General,
                        EditorLifecycleErrorCodes.EditorUnavailable,
                        "The fixed Unity host exited."),
                    start,
                    lifecycleActionDispatched: false,
                    new LifecycleExecutionHostExitObservation(
                        start.Host.Process))),
            timeProvider: new ManualTimeProvider(observedAtUtc),
            hostExitTerminalizer: terminalizer);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Equal(
            LifecycleExecutionErrorCodes.UnityExited,
            failed.Failure.Code);
        Assert.Same(terminalReference, failed.LifecycleExecutionRef);
        Assert.Equal(
            ExecutionApplicationState.NotApplied,
            failed.ApplicationState);
        var invocation = Assert.Single(terminalizer.Invocations);
        Assert.IsType<CompileLifecycleExecutionTerminalRecord>(
            invocation.TerminalRecord);
        Assert.Same(start, invocation.Start);
        Assert.Same(start.LifecycleExecutionRef, invocation.CurrentReference);
        Assert.Equal(
            LifecycleExecutionTerminalReason.UnityExited,
            invocation.TerminalReason);
        Assert.Equal(
            ExecutionApplicationState.NotApplied,
            invocation.ApplicationState);
        Assert.Equal(observedAtUtc, invocation.CompletedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenHostExitTerminalPublicationFails_PreservesFixedTypedResult ()
    {
        var start = CreateStart();
        var typedResult = CreateResult();
        var publishingReference = CreatePublishingReference();
        var fixedTerminalRecord = CreateCompletedTerminalRecord(
            typedResult,
            Verdict.Pass);
        var terminalizer = new RecordingLifecycleExecutionHostExitTerminalizer(
            new LifecycleExecutionHostExitTerminalizationResult.PublicationFailed(
                publishingReference,
                ExecutionApplicationState.Applied,
                ApplicationFailure.InternalError(
                    "Terminal Record publication failed.",
                    LifecycleExecutionErrorCodes.TerminalPublicationFailed),
                fixedTerminalRecord));
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(
                UnityRequestExecutionResult.Failure(
                    new UnityRequestFailure(
                        UnityRequestFailureKind.General,
                        EditorLifecycleErrorCodes.EditorUnavailable,
                        "The fixed Unity host exited."),
                    start,
                    lifecycleActionDispatched: true,
                    new LifecycleExecutionHostExitObservation(
                        start.Host.Process))),
            timeProvider: new ManualTimeProvider(
                start.StartedAtUtc.AddSeconds(1)),
            hostExitTerminalizer: terminalizer);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Equal(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            failed.Failure.Code);
        Assert.Same(publishingReference, failed.LifecycleExecutionRef);
        Assert.Equal(ExecutionApplicationState.Applied, failed.ApplicationState);
        Assert.Same(typedResult, failed.Result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenLifecycleStartRejectsDefinition_PreservesTypedStartErrorWithoutActionPayload ()
    {
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(new { }),
            [
                new OperationExecutionError(
                    LifecycleExecutionErrorCodes.DefinitionConflict,
                    "Lifecycle Execution definition conflicts with the durable start.",
                    "/definitionDigest"),
            ]);
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(
                UnityRequestExecutionResult.Success(response)));

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Equal(
            LifecycleExecutionErrorCodes.DefinitionConflict,
            failed.Failure.Code);
        Assert.Equal("/definitionDigest", failed.Failure.InstancePath);
        Assert.Null(failed.LifecycleExecutionRef);
        Assert.Equal(
            ExecutionApplicationState.NotApplied,
            failed.ApplicationState);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTerminalPublicationFails_PreservesTypedResultAndObservedLifecycle ()
    {
        var typedResult = CreateResult();
        var observedLifecycle = UnityEditorObservationTestFactory.Create(
            projectFingerprint: ProjectContextTestFactory.ProjectFingerprint,
            generations: new UnityEditorGenerationSnapshot(
                CompileGeneration: 14,
                DomainReloadGeneration: 7,
                AssetRefreshGeneration: 3,
                PlayModeGeneration: 2),
            observedAtUtc: StartedAtUtc.AddSeconds(3));
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(
                CreateCompileErrorResponseResult(
                    ExecutionApplicationState.Applied,
                    new OperationExecutionError(
                        LifecycleExecutionErrorCodes.TerminalPublicationFailed,
                        "Compile terminal record could not be published.",
                        InstancePath: null),
                    CreatePublishingReference(),
                    typedResult,
                    observedLifecycle)));

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Equal(ExecutionId, failed.LifecycleExecutionRef!.Id);
        Assert.Equal(ExecutionLifecycle.Recovery, failed.LifecycleExecutionRef.Lifecycle);
        Assert.Equal(ExecutionApplicationState.Applied, failed.ApplicationState);
        Assert.Equal(LifecycleExecutionErrorCodes.TerminalPublicationFailed, failed.Failure.Code);
        Assert.Equal(typedResult, failed.Result);
        Assert.Equal(observedLifecycle, failed.ObservedLifecycle);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithInvalidResponseAfterRegistration_ReturnsUnknownApplicationState ()
    {
        using var document = JsonDocument.Parse("""{"result":null}""");
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(
                UnityRequestExecutionResult.Success(
                    new UnityRequestResponse(document.RootElement.Clone(), []),
                    CreateStart())));

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Equal(ExecutionId, failed.LifecycleExecutionRef!.Id);
        Assert.Equal(ExecutionApplicationState.Unknown, failed.ApplicationState);
        Assert.Null(failed.Result);
        Assert.Null(failed.ObservedLifecycle);
        Assert.Contains(
            "Unity compile payload is invalid.",
            failed.Failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMismatchedTerminalReference_ReturnsRegisteredReferenceAsUnknown ()
    {
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(
                new IpcCompileResponse(
                    CreateTerminalReference(OtherExecutionId),
                    CreateResult())),
            []);
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(
                UnityRequestExecutionResult.Success(response, CreateStart())));

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Equal(ExecutionId, failed.LifecycleExecutionRef!.Id);
        Assert.Equal(ExecutionApplicationState.Unknown, failed.ApplicationState);
        Assert.Null(failed.Result);
        Assert.Null(failed.ObservedLifecycle);
        Assert.Contains(
            "different Lifecycle Execution",
            failed.Failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenIpcResultDiffersFromReverifiedTerminalRecord_RejectsSuccessPayload ()
    {
        var ipcResult = CreateResult();
        var terminalResult = CreateResult(errorCount: 1);
        var reconnectResolver =
            new RecordingLifecycleExecutionReconnectResolver(
                CreateTerminalResolution(
                    terminalResult,
                    Verdict.Fail));
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(
                CreateCompileResponseResult(ipcResult)),
            reconnectResolver: reconnectResolver);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Equal(UcliCoreErrorCodes.InternalError, failed.Failure.Code);
        Assert.Contains(
            "does not match its reverified Terminal Record",
            failed.Failure.Message,
            StringComparison.Ordinal);
        Assert.Equal(ipcResult, failed.Result);
        Assert.Equal(
            ExecutionApplicationState.Applied,
            failed.ApplicationState);
        Assert.Equal(
            ExecutionLifecycle.Recovery,
            failed.LifecycleExecutionRef!.Lifecycle);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Publishing),
            failed.LifecycleExecutionRef.State.Value);
        Assert.Equal(
            ExecutionId,
            failed.LifecycleExecutionRef.Id);
        Assert.Single(reconnectResolver.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WhenSuccessfulResponseTerminalPublicationCannotBeReverified_PreservesTypedResultAndRecoveryReference ()
    {
        await AssertSuccessfulResponseReverificationFailureAsync(
            recoveryReference =>
                new LifecycleExecutionReconnectResolution.PublicationFailed(
                    ApplicationFailure.InternalError(
                        "Compile Terminal Record publication could not be reverified.",
                        LifecycleExecutionErrorCodes.TerminalPublicationFailed),
                    recoveryReference));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WhenSuccessfulResponseTerminalReferenceIsRejected_PreservesTypedResultAndRecoveryReference ()
    {
        await AssertSuccessfulResponseReverificationFailureAsync(
            _ => new LifecycleExecutionReconnectResolution.Rejected(
                ApplicationFailure.InvalidInput(
                    "Compile terminal reference was rejected during reverification.",
                    UcliCoreErrorCodes.InvalidArgument)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenModeDecisionConsumesDeadline_DoesNotRegisterOrDispatch ()
    {
        var timeProvider = new ManualTimeProvider(StartedAtUtc);
        var modeDecisionService = new StubModeDecisionService(
            UnityExecutionModeDecisionResult.Success(
                new UnityExecutionModeDecision(
                    UnityExecutionMode.Oneshot,
                    DaemonRunning: false,
                    UnityExecutionTarget.Oneshot,
                    TimeSpan.FromSeconds(10))))
        {
            TimeProvider = timeProvider,
            OnDecide = _ => timeProvider.Advance(TimeSpan.FromSeconds(10)),
        };
        var executor = new RecordingUnityRequestExecutor(
            CreateCompileResponseResult(CreateResult()));
        var service = CreateService(
            modeDecisionService: modeDecisionService,
            unityRequestExecutor: executor,
            timeProvider: timeProvider);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Oneshot,
            TimeoutMilliseconds: 10000));

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Null(failed.LifecycleExecutionRef);
        Assert.Equal(ExecutionApplicationState.NotApplied, failed.ApplicationState);
        Assert.Null(failed.Result);
        Assert.Null(failed.ObservedLifecycle);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, failed.Failure.Code);
        Assert.Empty(executor.Invocations);
    }

    private static async Task
        AssertSuccessfulResponseReverificationFailureAsync (
            Func<ExecutionRef, LifecycleExecutionReconnectResolution>
                createTerminalResolution)
    {
        ArgumentNullException.ThrowIfNull(createTerminalResolution);
        var typedResult = CreateResult();
        var start = CreateStart();
        var recoveryReference = CreatePublishingReference();
        var registration = new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(
                LifecycleExecutionKind.Compile),
            ExecutionId,
            start.DeadlineUtc,
            start.StartedAtUtc);
        var terminalResolution =
            createTerminalResolution(recoveryReference);
        var reconnectResolver =
            new RecordingLifecycleExecutionReconnectResolver(
                new LifecycleExecutionReconnectResolution.Open(
                    registration,
                    recoveryReference,
                    start),
                terminalResolution);
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(
                CreateCompileResponseResult(typedResult)),
            reconnectResolver: reconnectResolver,
            executionIdGenerator: new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            new CompileCommandInput(
                ProjectPath: null,
                Mode: UnityExecutionMode.Oneshot,
                TimeoutMilliseconds: 10000),
            recoveryReference,
            cancellationToken: CancellationToken.None);

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Same(recoveryReference, failed.LifecycleExecutionRef);
        Assert.Equal(
            ExecutionLifecycle.Recovery,
            failed.LifecycleExecutionRef!.Lifecycle);
        Assert.Equal(
            ExecutionApplicationState.Applied,
            failed.ApplicationState);
        Assert.Equal(typedResult, failed.Result);
        Assert.Null(failed.ObservedLifecycle);
        Assert.Equal(2, reconnectResolver.Invocations.Count);
    }
}
