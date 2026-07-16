using System.Text.Json;
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
        var operationResultPayload = JsonSerializer.SerializeToElement(new
        {
            ok = true,
        });
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            OperationExecuteServiceTestSupport.CreateCallSuccessResult(
                result: operationResultPayload,
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
            timeProvider: timeProvider);

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
        Assert.Equal("refresh", opResult.OpId.Value);
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, opResult.Op);
        Assert.Equal(IpcExecuteOperationPhase.Call, opResult.Phase);
        Assert.True(opResult.Applied);
        Assert.True(opResult.Changed);
        Assert.Equal(JsonValueKind.Object, opResult.Result!.Value.ValueKind);
        var touchedResource = Assert.Single(opResult.Touched);
        Assert.Equal(UcliTouchedResourceKind.Asset, touchedResource.Kind);
        Assert.Equal("Assets/Example.txt", touchedResource.Path);
        Assert.Equal(Guid.ParseExact("11111111111111111111111111111111", "N"), touchedResource.AssetGuid);

        OperationExecuteInvocationAssert.AuthorizationCheckedOnce(
            authorizationService,
            UcliPrimitiveOperationNames.ProjectRefresh,
            OperationPolicy.Advanced);

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
        Assert.Equal(JsonValueKind.Object, executeRequest.Args.ValueKind);
    }
}
