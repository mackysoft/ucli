using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

using static MackySoft.Ucli.Application.Tests.PlanServiceTestSupport;

public sealed class PlanServiceUnityResponseTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityResponseOmitsPlanToken_ReturnsInternalErrorWithPartialOpResults ()
    {
        var unityIpcRequestExecutor = new RecordingUnityRequestExecutor(CreatePlanSuccess(
            planToken: null,
            opResults:
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
        var service = CreateOpService(
            CreateOperationDescriptor(),
            unityIpcRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput() with
            {
                RequestJson = OpRequestJson,
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Single(result.Output!.OpResults);
        Assert.Null(result.Output.PlanToken);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityResponseContainsResultForNoOpRequest_RejectsUntrustedResults ()
    {
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(CreatePlanSuccess(
                "plan-token-1",
                [
                    new IpcExecuteOperationResult(
                        Op: UcliPrimitiveOperationNames.GoDescribe,
                        Phase: IpcExecuteOperationPhase.Plan,
                        Applied: false,
                        Changed: false,
                        Touched: [],
                        OperationDescriptorDigest: OperationDescriptorDigest,
                        Verdict: null,
                        Result: null,
                        Diagnostics: []),
                ])));

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Empty(result.Output!.OpResults);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenOperationDescriptorDigestDoesNotMatch_RejectsUntrustedResults ()
    {
        var service = CreateOpService(
            CreateOperationDescriptor(),
            new RecordingUnityRequestExecutor(CreatePlanSuccess(
                "plan-token-1",
                [
                    new IpcExecuteOperationResult(
                        Op: UcliPrimitiveOperationNames.GoDescribe,
                        Phase: IpcExecuteOperationPhase.Plan,
                        Applied: false,
                        Changed: false,
                        Touched: [],
                        OperationDescriptorDigest: Sha256Digest.Compute("other descriptor"u8),
                        Verdict: null,
                        Result: null,
                        Diagnostics: []),
                ])));

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput() with
            {
                RequestJson = OpRequestJson,
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Empty(result.Output!.OpResults);
        var error = Assert.Single(result.Errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenOperationResultViolatesRegisteredSchema_RejectsUntrustedResults ()
    {
        var operationDescriptor = CreateResultfulOperationDescriptor(
            """{"type":"object","properties":{"value":{"type":"integer"}},"required":["value"],"additionalProperties":false}""");
        var service = CreateOpService(
            operationDescriptor,
            new RecordingUnityRequestExecutor(CreatePlanSuccess(
                "plan-token-1",
                [
                    new IpcExecuteOperationResult(
                        Op: UcliPrimitiveOperationNames.GoDescribe,
                        Phase: IpcExecuteOperationPhase.Plan,
                        Applied: false,
                        Changed: false,
                        Touched: [],
                        OperationDescriptorDigest: OperationDescriptorDigest,
                        Verdict: null,
                        Result: JsonSerializer.SerializeToElement(new
                        {
                            value = "not-an-integer",
                        }),
                        Diagnostics: []),
                ])));

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput() with
            {
                RequestJson = OpRequestJson,
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Empty(result.Output!.OpResults);
        var error = Assert.Single(result.Errors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public Task Execute_WhenUnityExecutionFails_ReturnsToolErrorAndPreservesPayload ()
    {
        return AssertUnityExecutionFailureAsync(
            new UnityRequestFailure(
                UnityRequestFailureKind.General,
                EditorLifecycleErrorCodes.EditorPlaymode,
                "Unity execution failed.",
                startupFailure: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public Task Execute_WhenUnityExecutionTimesOut_ReturnsToolErrorAndPreservesPayload ()
    {
        return AssertUnityExecutionFailureAsync(
            new UnityRequestFailure(
                UnityRequestFailureKind.General,
                ExecutionErrorCodes.IpcTimeout,
                "Unity execution failed.",
                startupFailure: null));
    }

    private static async Task AssertUnityExecutionFailureAsync (UnityRequestFailure failure)
    {
        var service = CreateService(
            unityRequestExecutor: new RecordingUnityRequestExecutor(
                UnityRequestExecutionResult.Failure(failure)));

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Equal(RequestId, result.Output!.RequestId);
        Assert.NotNull(result.Output.ReadIndex);
        var error = Assert.Single(result.Errors);
        Assert.Equal(failure.Code, error.Code);
    }

}
