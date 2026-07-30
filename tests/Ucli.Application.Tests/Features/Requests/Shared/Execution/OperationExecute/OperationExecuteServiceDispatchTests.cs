using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.OperationExecute;

public sealed class OperationExecuteServiceDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAuthorizationAndUnityExecutionSucceed_UsesFixedOperationRequest ()
    {
        var projectContextResolver = OperationExecuteServiceTestSupport.CreateProjectContextResolver();
        var authorizationService = OperationExecuteServiceTestSupport.CreateAllowedAuthorizationService();
        var timeProvider = new ManualTimeProvider();
        var operationCatalog = new RecordingOperationCatalog
        {
            Operations =
            [
                OperationExecuteServiceTestSupport.RefreshDescriptor,
            ],
        };
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            OperationExecuteServiceTestSupport.CreateCallSuccessResult(
                touched:
                [
                    new IpcExecuteTouchedResource(
                        kind: UcliTouchedResourceKind.Asset,
                        path: "Assets/Example.txt",
                        assetGuid: Guid.ParseExact("11111111111111111111111111111111", "N")),
                ]));
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            ipcRequestExecutor,
            timeProvider: timeProvider,
            operationCatalog: operationCatalog);

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RequestId,
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: UnityExecutionMode.Daemon,
                timeoutMilliseconds: 120000,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.Equal(OperationExecuteServiceTestSupport.RequestId, result.RequestId);
        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.Success, result.Outcome);
        Assert.Empty(result.Errors);
        var opResult = Assert.Single(result.OpResults);
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, opResult.Op);
        Assert.Equal(IpcExecuteOperationPhase.Call, opResult.Phase);
        Assert.True(opResult.Applied);
        Assert.True(opResult.Changed);
        Assert.Null(opResult.Result);
        var touchedResource = Assert.Single(opResult.Touched);
        Assert.Equal(UcliTouchedResourceKind.Asset, touchedResource.Kind);
        Assert.Equal("Assets/Example.txt", touchedResource.Path);
        Assert.Equal(Guid.ParseExact("11111111111111111111111111111111", "N"), touchedResource.AssetGuid);

        OperationExecuteInvocationAssert.AuthorizationCheckedOnce(
            authorizationService,
            UcliPrimitiveOperationNames.ProjectRefresh,
            OperationPolicy.Advanced);
        var catalogInvocation = Assert.Single(operationCatalog.ProjectGetAllInvocations);
        Assert.Equal(UnityExecutionMode.Daemon, catalogInvocation.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(120000), catalogInvocation.Timeout);
        Assert.True(catalogInvocation.FailFast);

        var execution = OperationExecuteInvocationAssert.CallDispatched(
            ipcRequestExecutor,
            UcliCommandIds.Refresh,
            UnityExecutionMode.Daemon,
            TimeSpan.FromMilliseconds(120000),
            expectedRepositoryRoot: ProjectPathTestValues.RepositoryRoot,
            expectedFailFast: true,
            expectedOperationId: "refresh",
            expectedOperationName: UcliPrimitiveOperationNames.ProjectRefresh);
        var executeRequest = execution.Request;
        Assert.Equal(System.Text.Json.JsonValueKind.Object, executeRequest.Args.ValueKind);
    }
}
