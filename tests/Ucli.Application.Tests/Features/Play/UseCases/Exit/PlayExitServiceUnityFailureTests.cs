using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using static MackySoft.Ucli.Application.Tests.Play.PlayExitServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayExitServiceUnityFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WhenTheFixedHostResponseIsInterrupted_RetainsTheDurableExecutionReference ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.TransportInterrupted,
                    EditorLifecycleErrorCodes.EditorUnavailable,
                    "The fixed host was interrupted."),
                CreateStartBinding(),
                lifecycleActionDispatched: true));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.StartAsync(
            await CreateStartInvocationAsync(requestExecutor));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionId, result.FailureContext!.LifecycleExecutionRef.Id);
        Assert.Equal(ExecutionApplicationState.Indeterminate, result.FailureContext.ApplicationState);
    }
}
