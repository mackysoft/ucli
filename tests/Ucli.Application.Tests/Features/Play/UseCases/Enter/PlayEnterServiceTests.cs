using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using static MackySoft.Ucli.Application.Tests.Play.PlayEnterServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayEnterServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_UsesTheCallerFixedBindingAndReturnsTheEnteredTransition ()
    {
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(CreateEnteredResponse()),
                CreateStartBinding()));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.StartAsync(
            await CreateStartInvocationAsync(requestExecutor));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(PlayLifecycleTransitionOutcome.Entered, result.Output!.Transition.Result);
        var provider = Assert.Single(requestExecutor.Invocations);
        Assert.Equal(UcliCommandIds.PlayEnter, provider.Command);
        Assert.Equal(TimeSpan.FromMilliseconds(4500), provider.Timeout);
    }
}
