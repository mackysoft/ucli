using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using static MackySoft.Ucli.Application.Tests.Play.PlayEnterServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayEnterServiceReconnectTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WhenTerminalPublicationFails_RetainsPublishingReference ()
    {
        var start = CreateStartBinding();
        var publishingReference = new RecoveryExecutionRef(
            start.LifecycleExecutionRef.Kind,
            start.LifecycleExecutionRef.Id,
            start.LifecycleExecutionRef.DefinitionDigest,
            new ExecutionState(TextVocabulary.GetText(
                LifecycleExecutionState.Publishing)),
            start.LifecycleExecutionRef.StatusLocator);
        var reconnectResolver =
            new RecordingLifecycleExecutionReconnectResolver(
                new LifecycleExecutionReconnectResolution.PublicationFailed(
                    ApplicationFailure.InternalError(
                        "Terminal Record publication failed.",
                        LifecycleExecutionErrorCodes.TerminalPublicationFailed),
                    publishingReference));
        var requestExecutor = new RecordingUnityRequestExecutor(
            _ => throw new InvalidOperationException(
                "A failed Terminal Record publication must not dispatch the Play Mode enter provider."));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor,
            reconnectResolver,
            new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            new PlayEnterCommandInput(
                ProjectPath: null,
                TimeoutMilliseconds: 1500),
            start.LifecycleExecutionRef,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            result.Error!.Code);
        Assert.Same(
            publishingReference,
            result.FailureContext!.LifecycleExecutionRef);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            result.FailureContext.ApplicationState);
        Assert.Empty(requestExecutor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenHostExitIsObservedAtDeadline_PublishesDeadlineTerminal ()
    {
        var start = CreateStartBinding();
        var terminalReference = CreateFailedTerminalReference();
        var terminalRecord = new PlayEnterLifecycleExecutionTerminalRecord(
            ExecutionId,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            terminalGeneration: null,
            start.DeadlineUtc,
            start.StartedAtUtc,
            start.DeadlineUtc,
            LifecycleExecutionTerminalReason.DeadlineExceeded,
            ExecutionApplicationState.Indeterminate,
            result: null,
            verdict: null,
            Array.Empty<ArtifactRef>());
        var terminalizer =
            new RecordingLifecycleExecutionHostExitTerminalizer(
                new LifecycleExecutionHostExitTerminalizationResult.Published(
                    terminalReference,
                    terminalRecord));
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    EditorLifecycleErrorCodes.EditorUnavailable,
                    "The fixed Unity host exited."),
                start,
                lifecycleActionDispatched: false,
                new LifecycleExecutionHostExitObservation(
                    start.Host.Process)));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor,
            hostExitTerminalizer: terminalizer,
            timeProvider: new FakeTimeProvider(
                start.DeadlineUtc));

        var result = await service.ExecuteAsync(
            new PlayEnterCommandInput(
                ProjectPath: null,
                TimeoutMilliseconds: 1500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.DeadlineExceeded,
            result.Error!.Code);
        Assert.Same(
            terminalReference,
            result.FailureContext!.LifecycleExecutionRef);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            result.FailureContext.ApplicationState);
        Assert.Null(result.FailureContext.CurrentLifecycle);
        var invocation = Assert.Single(terminalizer.Invocations);
        Assert.IsType<PlayEnterLifecycleExecutionTerminalRecord>(
            invocation.TerminalRecord);
        Assert.Equal(
            LifecycleExecutionTerminalReason.DeadlineExceeded,
            invocation.TerminalReason);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            invocation.ApplicationState);
        Assert.Equal(start.DeadlineUtc, invocation.CompletedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WithResultlessUnityExitedTerminal_ReturnsTerminalStartContextWithoutProviderDispatch ()
    {
        var start = CreateStartBinding();
        var registration = CreateOriginalRegistration(start);
        var terminalReference = CreateFailedTerminalReference();
        var terminalRecord = new PlayEnterLifecycleExecutionTerminalRecord(
            ExecutionId,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            terminalGeneration: null,
            start.DeadlineUtc,
            start.StartedAtUtc,
            start.StartedAtUtc.AddSeconds(1),
            LifecycleExecutionTerminalReason.UnityExited,
            ExecutionApplicationState.Indeterminate,
            result: null,
            verdict: null,
            Array.Empty<ArtifactRef>());
        var reconnectResolver =
            new RecordingLifecycleExecutionReconnectResolver(
                new LifecycleExecutionReconnectResolution.Terminal(
                    terminalReference,
                    terminalRecord));
        var requestExecutor = new RecordingUnityRequestExecutor(
            _ => throw new InvalidOperationException(
                "A terminal Play Mode enter execution must not reconnect through a provider."));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor,
            reconnectResolver,
            new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            new PlayEnterCommandInput(
                ProjectPath: null,
                TimeoutMilliseconds: 1500),
            terminalReference,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            LifecycleExecutionErrorCodes.UnityExited,
            result.Error!.Code);
        Assert.Same(
            terminalReference,
            result.FailureContext!.LifecycleExecutionRef);
        Assert.Null(result.FailureContext.CurrentLifecycle);
        Assert.Null(result.FailureContext.Transition);
        Assert.Empty(requestExecutor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_UsesOriginalRegistrationAndCollectsTerminalThroughPlayEnterHandler ()
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
                CreateResponse(CreateEnteredResponse()),
                start));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor,
            reconnectResolver,
            new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            new PlayEnterCommandInput(
                ProjectPath: null,
                TimeoutMilliseconds: 1500),
            publishedReference,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExecutionId, result.Output!.LifecycleExecutionRef.Id);
        var invocation = Assert.Single(requestExecutor.Invocations);
        var payload = Assert.IsType<UnityRequestPayload.PlayEnter>(
            invocation.Payload);
        Assert.Same(originalRegistration, payload.Registration);
        Assert.Equal(TimeSpan.FromMilliseconds(4500), invocation.Timeout);
        var resolution = Assert.Single(reconnectResolver.Invocations);
        Assert.Equal(
            LifecycleExecutionKind.PlayEnter,
            resolution.ExpectedDefinition.Kind);
        Assert.Same(publishedReference, resolution.ExecutionRef);
    }

    private static LifecycleExecutionRegistration CreateOriginalRegistration (
        LifecycleExecutionStartBinding start)
    {
        return new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(
                LifecycleExecutionKind.PlayEnter),
            start.LifecycleExecutionRef.Id,
            start.DeadlineUtc,
            start.StartedAtUtc);
    }
}
