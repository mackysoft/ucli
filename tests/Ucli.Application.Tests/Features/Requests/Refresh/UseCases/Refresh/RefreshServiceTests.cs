using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Refresh;

public sealed class RefreshServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_CreatesRefreshOperationDefinitionAndReturnsExecuteResult ()
    {
        var requestId = Guid.Parse("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");
        var operationExecuteService = new RecordingOperationExecuteService(OperationExecuteResultFactory.Success(
            requestId,
            [],
            "uCLI refresh completed.",
            readPostcondition: null,
            project: ProjectIdentityInfoTestFactory.CreateRepositoryFixture()));
        var service = new RefreshService(operationExecuteService);

        var result = await service.ExecuteAsync(
            requestId,
            new RefreshCommandInput(
                ProjectPath: "/repo/UnityProject",
                Mode: UnityExecutionMode.Oneshot,
                TimeoutMilliseconds: 1234,
                FailFast: true),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(requestId, result.RequestId);
        OperationExecuteServiceInvocationAssert.ExecutedOnce(
            operationExecuteService,
            UcliCommandIds.Refresh,
            expectedOperationId: "refresh",
            expectedOperationName: UcliPrimitiveOperationNames.ProjectRefresh,
            expectedKind: UcliOperationKind.Command,
            expectedPolicy: OperationPolicy.Advanced,
            expectedSuccessMessage: "uCLI refresh completed.",
            expectedFailureMessage: "uCLI refresh failed.",
            expectedProjectPath: "/repo/UnityProject",
            expectedMode: UnityExecutionMode.Oneshot,
            expectedTimeoutMilliseconds: 1234,
            expectedFailFast: true);
    }
}
