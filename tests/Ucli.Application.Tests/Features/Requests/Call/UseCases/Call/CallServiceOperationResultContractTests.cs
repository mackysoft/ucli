using MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.CallServiceTestSupport;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;

namespace MackySoft.Ucli.Application.Tests;

public sealed class CallServiceOperationResultContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlanResponseDoesNotMatchPreparedSteps_RejectsResponseBeforeCall ()
    {
        var preparedRequest = CreateSingleOperationPreparedRequest(
            UcliPrimitiveOperationNames.GoDescribe,
            OperationPolicy.Safe);
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateUnityResponse(
                    IpcResponseStatus.Ok,
                    [
                        new IpcExecuteOperationResult(
                            Op: UcliPrimitiveOperationNames.AssetsFind,
                            Phase: IpcExecuteOperationPhase.Plan,
                            Applied: false,
                            Changed: false,
                            Touched: [],
                            OperationDescriptorDigest: OperationDescriptorDigest,
                            Verdict: null,
                            Result: null,
                            Diagnostics: []),
                    ],
                    errors: [],
                    planToken: "issued-plan-token")));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(withPlan: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Single(ipcRequestExecutor.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallResponseUsesAnotherDescriptorDigest_RejectsResponseAsInternalError ()
    {
        var preparedRequest = CreateSingleOperationPreparedRequest(
            UcliPrimitiveOperationNames.GoDescribe,
            OperationPolicy.Safe);
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateUnityResponse(
                    IpcResponseStatus.Ok,
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
                    ],
                    errors: [],
                    planToken: "issued-plan-token")),
            UnityRequestExecutionResult.Success(
                CreateUnityResponse(
                    IpcResponseStatus.Ok,
                    [
                        new IpcExecuteOperationResult(
                            Op: UcliPrimitiveOperationNames.GoDescribe,
                            Phase: IpcExecuteOperationPhase.Call,
                            Applied: true,
                            Changed: false,
                            Touched: [],
                            OperationDescriptorDigest: Sha256Digest.Compute("another operation descriptor"u8),
                            Verdict: null,
                            Result: null,
                            Diagnostics: []),
                    ],
                    errors: [])));
        var service = CreateService(
            PhaseExecutionPreflightResult.Success(preparedRequest),
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(withPlan: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.NotNull(result.Output);
        Assert.NotNull(result.Output!.Plan);
        Assert.Empty(result.Output.OpResults);
        Assert.Equal(2, ipcRequestExecutor.Invocations.Count);
    }

    private static CallCommandInput CreateInput (bool withPlan)
    {
        return new CallCommandInput(
            ProjectPath: AbsolutePath.Parse(ProjectPathTestValues.RepositoryUnityProject),
            Mode: NormalizeMode("oneshot"),
            TimeoutMilliseconds: NormalizeTimeout("1200"),
            PlanToken: null,
            WithPlan: withPlan,
            AllowDangerous: false,
            FailFast: false,
            RequestJson: """{"steps":[]}""");
    }
}
