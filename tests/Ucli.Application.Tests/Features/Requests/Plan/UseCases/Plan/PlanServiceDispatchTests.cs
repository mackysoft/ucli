using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
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
        var preparedRequest = CreateOpPreparedRequestContext();
        var operationDescriptor = CreateOperationDescriptor();
        var unityIpcRequestExecutor = new RecordingUnityRequestExecutor(CreatePlanSuccess(
            "plan-token-1",
            [
                new IpcExecuteOperationResult(
                    Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Phase: IpcExecuteOperationPhase.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: [],
                    OperationDescriptorDigest: OperationDescriptorDigest,
                    Verdict: null,
                    Result: null,
                    Diagnostics: []),
            ]));
        var service = CreateService(
            requestPreparationService: new RecordingRequestPreparationService
            {
                PrepareResult = RequestPreparationResult.Success(preparedRequest),
            },
            staticPreflightService: new RecordingRequestStaticValidationPreflightService
            {
                Result = CreateSuccessPreflightResult(
                    preparedRequest,
                    CreateReadIndexInfo(
                        used: true,
                        hit: true,
                        freshness: IndexFreshness.Probable,
                        fallbackReason: null),
                    RequestStaticValidationCatalog.Available([operationDescriptor])),
            },
            unityRequestExecutor: unityIpcRequestExecutor,
            timeProvider: new ManualTimeProvider());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1234,
                failFast: true) with
            {
                RequestJson = OpRequestJson,
            },
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
    }
}
