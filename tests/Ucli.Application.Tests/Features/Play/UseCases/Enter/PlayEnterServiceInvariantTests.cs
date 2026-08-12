using MackySoft.Ucli.Application.Features.Play.UseCases.Enter;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Play.PlayEnterServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Play;

public sealed class PlayEnterServiceInvariantTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task StartAsync_WithAResponseForAnotherExecution_RetainsTheTrustedDurableStart ()
    {
        var otherExecutionId = Guid.Parse("2bb6851e-f54f-474b-833d-f466b6fe2219");
        var response = CreateEnteredResponse();
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(new IpcPlayTransitionResponse(
                    CreateTerminalReference(otherExecutionId),
                    response.Result)),
                CreateStartBinding()));
        var service = CreateService(
            PlayProjectContext,
            CreateGuiSessionStore(),
            requestExecutor);

        var result = await service.StartAsync(
            await CreateStartInvocationAsync(requestExecutor));

        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InternalError, result.Error!.Code);
        Assert.Equal(ExecutionId, result.FailureContext!.LifecycleExecutionRef.Id);
        Assert.Null(result.FailureContext.Transition);
    }
}
