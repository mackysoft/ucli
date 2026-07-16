using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

using static MackySoft.Ucli.Application.Tests.PlanServiceTestSupport;

public sealed class PlanServiceDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStaticPreflightSucceeds_UsesPlanIpcPayloadAndReturnsSuccess ()
    {
        var unityIpcRequestExecutor = new RecordingUnityRequestExecutor(CreatePlanSuccess(
            "plan-token-1",
            [
                new IpcExecuteOperationResult(
                    OpId: new IpcExecuteStepId("step-1"),
                    Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Phase: IpcExecuteOperationPhase.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: []),
            ]));
        var service = CreateService(
            staticPreflightService: new RecordingRequestStaticValidationPreflightService
            {
                Result = CreateSuccessPreflightResult(
                    CreateReadIndexInfo(
                        used: true,
                        hit: true,
                        freshness: IndexFreshness.Probable,
                        fallbackReason: null)),
            },
            unityRequestExecutor: unityIpcRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1234,
                failFast: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("uCLI plan completed.", result.Message);
        Assert.NotNull(result.Output);
        Assert.Equal(RequestId, result.Output!.RequestId);
        Assert.Equal("plan-token-1", result.Output.PlanToken);
        Assert.True(result.Output.ReadIndex.Used);
        var execution = PlanServiceInvocationAssert.PlanDispatched(unityIpcRequestExecutor);
        Assert.Equal(UnityExecutionMode.Oneshot, execution.Invocation.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), execution.Invocation.Timeout);
        Assert.True(execution.Request.FailFast);
        Assert.False(execution.Request.AllowPlayMode);
        Assert.Null(execution.Request.PlanToken);
        Assert.False(execution.Request.ExecuteArguments.TryGetProperty("requestId", out _));
    }
}
