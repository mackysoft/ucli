using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using static MackySoft.Ucli.Application.Tests.Play.PlayEnterServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayEnterServiceReconnectTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReconnectAsync_WhenTerminalPublicationFailed_DoesNotDispatchAnotherProviderAction ()
    {
        var start = CreateStartBinding();
        var publishingReference = new RecoveryExecutionRef(
            start.LifecycleExecutionRef.Kind,
            start.LifecycleExecutionRef.Id,
            start.LifecycleExecutionRef.DefinitionDigest,
            new ExecutionState(TextVocabulary.GetText(LifecycleExecutionState.Publishing)),
            start.LifecycleExecutionRef.StatusLocator
                ?? throw new InvalidOperationException("The registered start must have a status locator."));
        var requestExecutor = new RecordingUnityRequestExecutor(
            _ => throw new InvalidOperationException("Publication failure must not dispatch."));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor,
            new RecordingLifecycleExecutionReconnectResolver(
                new LifecycleExecutionReconnectResolution.PublicationFailed(
                    ApplicationFailure.InternalError(
                        "Terminal Record publication failed.",
                        LifecycleExecutionErrorCodes.TerminalPublicationFailed),
                    publishingReference)),
            new UnexpectedGuidGenerator());

        var result = await service.ReconnectAsync(
            await CreateReconnectInvocationAsync(
                requestExecutor,
                start.LifecycleExecutionRef));

        Assert.False(result.IsSuccess);
        Assert.Equal(publishingReference, result.FailureContext!.LifecycleExecutionRef);
        Assert.Empty(requestExecutor.Invocations);
    }
}
