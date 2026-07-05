using System.Text.Json;
using MackySoft.Tests;
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
                        Kind: UcliTouchedResourceKindNames.Asset,
                        Path: "Assets/Example.txt",
                        Guid: "11111111111111111111111111111111"),
                ]));
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            ipcRequestExecutor,
            timeProvider: timeProvider);

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: UnityExecutionMode.Daemon,
                timeoutMilliseconds: 120000,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.True(Guid.TryParseExact(result.RequestId, "D", out _));
        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.Success, result.Outcome);
        Assert.Empty(result.Errors);
        var opResult = Assert.Single(result.OpResults);
        Assert.Equal("refresh", opResult.OpId);
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, opResult.Op);
        Assert.Equal(IpcExecuteOperationPhaseNames.Call, opResult.Phase);
        Assert.True(opResult.Applied);
        Assert.True(opResult.Changed);
        Assert.Equal(JsonValueKind.Object, opResult.Result!.Value.ValueKind);
        var touchedResource = Assert.Single(opResult.Touched);
        Assert.Equal(UcliTouchedResourceKindNames.Asset, touchedResource.Kind);
        Assert.Equal("Assets/Example.txt", touchedResource.Path);
        Assert.Equal("11111111111111111111111111111111", touchedResource.Guid);

        OperationExecuteInvocationAssert.AuthorizationCheckedOnce(
            authorizationService,
            UcliPrimitiveOperationNames.ProjectRefresh,
            OperationPolicy.Advanced);

        var execution = OperationExecuteInvocationAssert.CallDispatched(
            ipcRequestExecutor,
            UcliCommandIds.Refresh,
            UnityExecutionMode.Daemon,
            TimeSpan.FromMilliseconds(120000),
            expectedRepositoryRoot: "/repo",
            expectedRequestId: result.RequestId,
            expectedFailFast: true,
            expectedOperationId: "refresh",
            expectedOperationName: UcliPrimitiveOperationNames.ProjectRefresh);
        var executeRequest = execution.Request;
        Assert.Equal(JsonValueKind.Object, executeRequest.Args.ValueKind);
    }
}
