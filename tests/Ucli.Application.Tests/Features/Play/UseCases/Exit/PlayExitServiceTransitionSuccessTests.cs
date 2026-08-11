using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using static MackySoft.Ucli.Application.Tests.Play.PlayExitServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayExitServiceTransitionSuccessTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_UsesTheCallerFixedBindingAndReturnsTheExitedTransition ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(CreateExitedResponse()),
                CreateStartBinding()));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.StartAsync(
            await CreateStartInvocationAsync(requestExecutor));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(PlayLifecycleTransitionOutcome.Exited, result.Output!.Transition.Result);
        Assert.Equal(UcliCommandIds.PlayExit, Assert.Single(requestExecutor.Invocations).Command);
    }
}
