using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests.Execution.OperationExecute;

public sealed class OperationExecuteServicePlanTokenTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlanTokenModeIsRequired_IssuesPlanBeforeCallWithIssuedToken ()
    {
        var projectContextResolver = OperationExecuteServiceTestSupport.CreateProjectContextResolver(CreateRequiredPlanConfig());
        var authorizationService = OperationExecuteServiceTestSupport.CreateAllowedAuthorizationService();
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            OperationExecuteServiceTestSupport.CreatePlanSuccessResult("plan-token-1"),
            OperationExecuteServiceTestSupport.CreateCallSuccessResult(changed: false));
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 120000,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        OperationExecuteInvocationAssert.PlanThenCallDispatched(
            ipcRequestExecutor,
            UcliCommandIds.Refresh,
            expectedRequestId: result.RequestId,
            expectedPlanToken: "plan-token-1",
            expectedFailFast: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlanConsumesTimeoutBudget_PassesRemainingTimeoutToCall ()
    {
        var timeProvider = new ManualTimeProvider();
        var projectContextResolver = OperationExecuteServiceTestSupport.CreateProjectContextResolver(CreateRequiredPlanConfig());
        var authorizationService = OperationExecuteServiceTestSupport.CreateAllowedAuthorizationService();
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            OperationExecuteServiceTestSupport.CreatePlanSuccessResult("plan-token-1"),
            OperationExecuteServiceTestSupport.CreateCallSuccessResult(changed: false))
        {
            OnExecute = context =>
            {
                if (context.Index == 1)
                {
                    timeProvider.Advance(TimeSpan.FromMilliseconds(200));
                }
            },
        };
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            ipcRequestExecutor,
            timeProvider: timeProvider);

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1200,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        OperationExecuteInvocationAssert.PlanThenCallDispatchedWithTimeouts(
            ipcRequestExecutor,
            expectedPlanTimeout: TimeSpan.FromMilliseconds(1200),
            expectedCallTimeout: TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlanConsumesEntireTimeoutBudget_ReturnsTimeoutBeforeCall ()
    {
        var timeProvider = new ManualTimeProvider();
        var projectContextResolver = OperationExecuteServiceTestSupport.CreateProjectContextResolver(CreateRequiredPlanConfig());
        var authorizationService = OperationExecuteServiceTestSupport.CreateAllowedAuthorizationService();
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            OperationExecuteServiceTestSupport.CreatePlanSuccessResult("plan-token-1"))
        {
            OnExecute = context =>
            {
                if (context.Index == 1)
                {
                    timeProvider.Advance(TimeSpan.FromMilliseconds(1200));
                }
            },
        };
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            ipcRequestExecutor,
            timeProvider: timeProvider);

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1200,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        OperationExecuteInvocationAssert.PlanOnlyDispatched(ipcRequestExecutor, UcliCommandIds.Refresh);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Equal("Timed out before Unity IPC execute request could begin.", error.Message);
    }

    private static UcliConfig CreateRequiredPlanConfig ()
    {
        return UcliConfig.CreateDefault() with
        {
            PlanTokenMode = PlanTokenMode.Required,
        };
    }
}
