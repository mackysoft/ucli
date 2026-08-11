using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

public sealed class CompileServiceReconnectTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WhenTerminalPublicationFails_RetainsPublishingReference ()
    {
        var start = CreateStart();
        var publishingReference = new RecoveryExecutionRef(
            start.LifecycleExecutionRef.Kind,
            start.LifecycleExecutionRef.Id,
            start.LifecycleExecutionRef.DefinitionDigest,
            new ExecutionState(TextVocabulary.GetText(
                LifecycleExecutionState.Publishing)),
            start.LifecycleExecutionRef.StatusLocator
                ?? throw new InvalidOperationException("The registered start must have a status locator."));
        var reconnectResolver =
            new RecordingLifecycleExecutionReconnectResolver(
                new LifecycleExecutionReconnectResolution.PublicationFailed(
                    ApplicationFailure.InternalError(
                        "Terminal Record publication failed.",
                        LifecycleExecutionErrorCodes.TerminalPublicationFailed),
                    publishingReference));
        var requestExecutor = new RecordingUnityRequestExecutor(
            _ => throw new InvalidOperationException(
                "A failed Terminal Record publication must not dispatch the compile provider."));
        var service = CreateService(
            unityRequestExecutor: requestExecutor,
            reconnectResolver: reconnectResolver,
            executionIdGenerator: new UnexpectedGuidGenerator());

        var execution = await service.ReconnectAsync(
            new CompileCommandInput(
                ProjectPath: null,
                Mode: UnityExecutionMode.Oneshot,
                TimeoutMilliseconds: 10000),
            start.LifecycleExecutionRef,
            cancellationToken: CancellationToken.None);

        var failed =
            Assert.IsType<CompileExecutionResult.FailedResult>(execution);
        Assert.Equal(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            failed.Failure.Code);
        Assert.Same(publishingReference, failed.LifecycleExecutionRef);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            failed.ApplicationState);
        Assert.Empty(requestExecutor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
        public async Task ReconnectAsync_WithCompletedTerminalRecord_PreservesActionOwnedVerdictWithoutProviderDispatch ()
        {
            var start = CreateStart();
            var completeResult = CreateResult();
            var result = new CompileLifecycleResult(
                completeResult.Refresh,
                completeResult.ScriptCompilation,
                new CompileLifecycleResult.DomainReloadEvidence(
                    completeResult.DomainReload.ReloadRequired,
                    completeResult.DomainReload.ReloadObserved,
                    completeResult.DomainReload.GenerationBefore,
                    completeResult.DomainReload.GenerationAfter,
                    Settled: false),
                completeResult.Lifecycle);
            var terminalReference = CreateTerminalReference();
        var terminalRecord = new CompileLifecycleExecutionTerminalRecord(
            ExecutionId,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            result.Lifecycle.State!.Generations,
            start.DeadlineUtc,
            start.StartedAtUtc,
            StartedAtUtc.AddSeconds(5),
            LifecycleExecutionTerminalReason.Completed,
            ExecutionApplicationState.Applied,
            result,
            Verdict.Incomplete,
            Array.Empty<ArtifactRef>());
        var reconnectResolver = new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Terminal(
                terminalReference,
                terminalRecord));
        var requestExecutor = new RecordingUnityRequestExecutor(
            _ => throw new InvalidOperationException(
                "A completed compile Terminal Record must not reconnect through a provider."));
        var service = CreateService(
            unityRequestExecutor: requestExecutor,
            reconnectResolver: reconnectResolver,
            executionIdGenerator: new UnexpectedGuidGenerator());

        var execution = await service.ReconnectAsync(
            new CompileCommandInput(
                ProjectPath: null,
                Mode: UnityExecutionMode.Oneshot,
                TimeoutMilliseconds: 10000),
            terminalReference,
            cancellationToken: CancellationToken.None);

        var completed =
            Assert.IsType<CompileExecutionResult.CompletedResult>(execution);
        Assert.Same(terminalReference, completed.Output.LifecycleExecutionRef);
        Assert.Equal(Verdict.Incomplete, completed.Output.Verdict);
        Assert.Empty(requestExecutor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_UsesOriginalRegistrationAndSameCompileHandler ()
    {
        var originalRegistration = CreateOriginalRegistration();
        var start = CreateStart();
        var publishedReference = start.LifecycleExecutionRef;
        var reconnectResolver = new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Open(
                originalRegistration,
                publishedReference,
                start),
            CreateTerminalResolution(
                CreateResult(),
                Verdict.Pass));
        var requestExecutor = new RecordingUnityRequestExecutor(
            CreateCompileResponseResult(CreateResult()));
        var service = CreateService(
            unityRequestExecutor: requestExecutor,
            reconnectResolver: reconnectResolver,
            executionIdGenerator: new UnexpectedGuidGenerator());
        var result = await service.ReconnectAsync(
            new CompileCommandInput(
                ProjectPath: null,
                Mode: UnityExecutionMode.Oneshot,
                TimeoutMilliseconds: 10000),
            publishedReference,
            cancellationToken: CancellationToken.None);

        var completed = Assert.IsType<CompileExecutionResult.CompletedResult>(result);
        Assert.Equal(ExecutionId, completed.Output.LifecycleExecutionRef.Id);
        var invocation = Assert.Single(requestExecutor.Invocations);
        var payload = Assert.IsType<UnityRequestPayload.Compile>(
            invocation.Payload);
        Assert.Same(originalRegistration, payload.Registration);
        Assert.Same(start, payload.RequiredStart);
        Assert.Equal(
            TimeSpan.FromSeconds(13),
            invocation.Timeout);
        Assert.Equal(2, reconnectResolver.Invocations.Count);
        var resolution = reconnectResolver.Invocations[0];
        Assert.Equal(
            LifecycleExecutionKind.Compile,
            resolution.ExpectedDefinition.Kind);
        Assert.Same(publishedReference, resolution.ExecutionRef);
        Assert.True(
            originalRegistration.HasSameIdentity(resolution.ExecutionRef));
        var terminalReverification = reconnectResolver.Invocations[1];
        Assert.Equal(
            LifecycleExecutionKind.Compile,
            terminalReverification.ExpectedDefinition.Kind);
        Assert.IsType<TerminalExecutionRef>(
            terminalReverification.ExecutionRef);
        Assert.True(
            originalRegistration.HasSameIdentity(
                terminalReverification.ExecutionRef));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WhenActualCallerCancellationOccursAfterResolution_RetainsReferenceAsUnknown ()
    {
        using var callerCancellation = new CancellationTokenSource();
        var originalRegistration = CreateOriginalRegistration();
        var start = CreateStart();
        var publishedReference = start.LifecycleExecutionRef;
        var reconnectResolver = new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Open(
                originalRegistration,
                publishedReference,
                start))
        {
            OnResolve = _ => callerCancellation.Cancel(),
        };
        var requestExecutor = new RecordingUnityRequestExecutor(
            CreateCompileResponseResult(CreateResult()));
        var service = CreateService(
            unityRequestExecutor: requestExecutor,
            reconnectResolver: reconnectResolver,
            executionIdGenerator: new UnexpectedGuidGenerator());
        var result = await service.ReconnectAsync(
            new CompileCommandInput(
                ProjectPath: null,
                Mode: UnityExecutionMode.Oneshot,
                TimeoutMilliseconds: 10000),
            publishedReference,
            cancellationToken: callerCancellation.Token);

        var failed = Assert.IsType<CompileExecutionResult.FailedResult>(result);
        Assert.Same(publishedReference, failed.LifecycleExecutionRef);
        Assert.Equal(
            ExecutionApplicationState.Unknown,
            failed.ApplicationState);
        Assert.Equal(ExecutionErrorCodes.Canceled, failed.Failure.Code);
        Assert.Equal(ApplicationFailureKind.Canceled, failed.Failure.Kind);
        Assert.Null(failed.Result);
        Assert.Null(failed.ObservedLifecycle);
        Assert.Empty(requestExecutor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_DoesNotReevaluateModeAfterResolvingStart ()
    {
        var originalRegistration = CreateOriginalRegistration();
        var start = CreateStart();
        var publishedReference = start.LifecycleExecutionRef;
        var reconnectResolver = new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Open(
                originalRegistration,
                publishedReference,
                start),
            CreateTerminalResolution(
                CreateResult(),
                Verdict.Pass));
        var requestExecutor = new RecordingUnityRequestExecutor(
            CreateCompileResponseResult(CreateResult()));
        var modeDecisionService = new StubModeDecisionService(
            UnityExecutionModeDecisionResult.Failure(
                ExecutionError.Timeout(
                    "Execution mode decision must not run during reconnect.",
                    ExecutionErrorCodes.IpcTimeout)));
        var service = CreateService(
            modeDecisionService: modeDecisionService,
            unityRequestExecutor: requestExecutor,
            reconnectResolver: reconnectResolver,
            executionIdGenerator: new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            new CompileCommandInput(
                ProjectPath: null,
                Mode: UnityExecutionMode.Oneshot,
                TimeoutMilliseconds: 10000),
            publishedReference,
            cancellationToken: CancellationToken.None);

        Assert.IsType<CompileExecutionResult.CompletedResult>(result);
        Assert.Empty(modeDecisionService.Invocations);
        var payload = Assert.IsType<UnityRequestPayload.Compile>(
            Assert.Single(requestExecutor.Invocations).Payload);
        Assert.Same(start, payload.RequiredStart);
    }

    private static LifecycleExecutionRegistration CreateOriginalRegistration ()
    {
        return new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(LifecycleExecutionKind.Compile),
            ExecutionId,
            StartedAtUtc.AddSeconds(10),
            StartedAtUtc);
    }
}
