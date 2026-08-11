using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using static MackySoft.Ucli.Application.Tests.Play.PlayExitServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayExitServiceReconnectTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenHostExitTerminalPublicationFails_RetainsFixedTransitionAndPublishingReference ()
    {
        var start = CreateStartBinding();
        var typedResult =
            PlayExitLifecycleTransitionResult.FromProviderResult(
                CreateExitedResponse().Result);
        var publishingReference = new RecoveryExecutionRef(
            start.LifecycleExecutionRef.Kind,
            start.LifecycleExecutionRef.Id,
            start.LifecycleExecutionRef.DefinitionDigest,
            new ExecutionState(TextVocabulary.GetText(
                LifecycleExecutionState.Publishing)),
            start.LifecycleExecutionRef.StatusLocator
                ?? throw new InvalidOperationException("The registered start must have a status locator."));
        var fixedTerminalRecord = new PlayExitLifecycleExecutionTerminalRecord(
            ExecutionId,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            typedResult.After!.State.Generations,
            start.DeadlineUtc,
            start.StartedAtUtc,
            start.StartedAtUtc.AddSeconds(1),
            LifecycleExecutionTerminalReason.Completed,
            ExecutionApplicationState.Applied,
            typedResult,
            verdict: null,
            Array.Empty<ArtifactRef>());
        var terminalizer =
            new RecordingLifecycleExecutionHostExitTerminalizer(
                new LifecycleExecutionHostExitTerminalizationResult
                    .PublicationFailed(
                        publishingReference,
                        ExecutionApplicationState.Applied,
                        ApplicationFailure.InternalError(
                            "Terminal Record publication failed.",
                            LifecycleExecutionErrorCodes
                                .TerminalPublicationFailed),
                        fixedTerminalRecord));
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    EditorLifecycleErrorCodes.EditorUnavailable,
                    "The fixed Unity host exited."),
                start,
                lifecycleActionDispatched: true,
                new LifecycleExecutionHostExitObservation(
                    start.Host.Process)));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor,
            hostExitTerminalizer: terminalizer,
            timeProvider: new FakeTimeProvider(
                start.StartedAtUtc.AddSeconds(1)));

        var result = await service.ExecuteAsync(
            new PlayExitCommandInput(
                ProjectPath: null,
                TimeoutMilliseconds: 1500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            result.Error!.Code);
        Assert.Same(
            publishingReference,
            result.FailureContext!.LifecycleExecutionRef);
        Assert.Equal(
            ExecutionApplicationState.Applied,
            result.FailureContext.ApplicationState);
        Assert.Equal(
            ExecutionLifecycle.Recovery,
            result.FailureContext.LifecycleExecutionRef.Lifecycle);
        Assert.Equal(
            typedResult.After!.State.Generations,
            result.FailureContext.CurrentLifecycle!.Generations);
        Assert.Equal(
            PlayLifecycleTransitionOutcome.Exited,
            result.FailureContext.Transition!.Result);
        var invocation = Assert.Single(terminalizer.Invocations);
        Assert.IsType<PlayExitLifecycleExecutionTerminalRecord>(
            invocation.TerminalRecord);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WithCompletedTerminalRecord_ReplaysExitResultWithoutProviderDispatch ()
    {
        var start = CreateStartBinding();
        var registration = CreateOriginalRegistration(start);
        var typedResult =
            PlayExitLifecycleTransitionResult.FromProviderResult(
                CreateExitedResponse().Result);
        var terminalReference = CreateTerminalReference();
        var terminalRecord = new PlayExitLifecycleExecutionTerminalRecord(
            ExecutionId,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            typedResult.After!.State.Generations,
            start.DeadlineUtc,
            start.StartedAtUtc,
            start.StartedAtUtc.AddSeconds(1),
            LifecycleExecutionTerminalReason.Completed,
            ExecutionApplicationState.Applied,
            typedResult,
            verdict: null,
            Array.Empty<ArtifactRef>());
        var reconnectResolver =
            new RecordingLifecycleExecutionReconnectResolver(
                new LifecycleExecutionReconnectResolution.Terminal(
                    terminalReference,
                    terminalRecord));
        var requestExecutor = new RecordingUnityRequestExecutor(
            _ => throw new InvalidOperationException(
                "A completed Play Mode exit Terminal Record must not reconnect through a provider."));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor,
            reconnectResolver,
            new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            new PlayExitCommandInput(
                ProjectPath: null,
                TimeoutMilliseconds: 1500),
            terminalReference,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(
            terminalReference,
            result.Output!.LifecycleExecutionRef);
        Assert.Equal(
            PlayLifecycleTransitionOutcome.Exited,
            result.Output.Transition.Result);
        Assert.Empty(requestExecutor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_UsesOriginalRegistrationAndCollectsTerminalThroughPlayExitHandler ()
    {
        var start = CreateStartBinding();
        var originalRegistration = CreateOriginalRegistration(start);
        var publishedReference = start.LifecycleExecutionRef;
        var reconnectResolver = new RecordingLifecycleExecutionReconnectResolver(
            new LifecycleExecutionReconnectResolution.Open(
                originalRegistration,
                publishedReference,
                start));
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(CreateExitedResponse()),
                start));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor,
            reconnectResolver,
            new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            new PlayExitCommandInput(
                ProjectPath: null,
                TimeoutMilliseconds: 1500),
            publishedReference,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExecutionId, result.Output!.LifecycleExecutionRef.Id);
        var invocation = Assert.Single(requestExecutor.Invocations);
        var payload = Assert.IsType<UnityRequestPayload.PlayExit>(
            invocation.Payload);
        Assert.Same(originalRegistration, payload.Registration);
        Assert.Equal(TimeSpan.FromMilliseconds(4500), invocation.Timeout);
        var resolution = Assert.Single(reconnectResolver.Invocations);
        Assert.Equal(
            LifecycleExecutionKind.PlayExit,
            resolution.ExpectedDefinition.Kind);
        Assert.Same(publishedReference, resolution.ExecutionRef);
    }

    private static LifecycleExecutionRegistration CreateOriginalRegistration (
        LifecycleExecutionStartBinding start)
    {
        return new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(
                LifecycleExecutionKind.PlayExit),
            start.LifecycleExecutionRef.Id,
            start.DeadlineUtc,
            start.StartedAtUtc);
    }
}
