using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

using static MackySoft.Ucli.Application.Tests.PlanServiceTestSupport;

public sealed class PlanServicePreflightTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightAllowsSyntaxOnlyFallback_ContinuesToUnityExecution ()
    {
        var unityIpcRequestExecutor = new RecordingUnityRequestExecutor(CreatePlanSuccess("plan-token-1"));
        var service = CreateService(
            staticPreflightService: new RecordingRequestStaticValidationPreflightService
            {
                Result = CreateSuccessPreflightResult(
                    CreateReadIndexInfo(
                        used: false,
                        hit: false,
                        freshness: IndexFreshness.Probable,
                        fallbackReason: "Index contract file was not found: ops.catalog.json.")),
            },
            unityRequestExecutor: unityIpcRequestExecutor);

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(readIndexMode: ReadIndexMode.AllowStale),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
        Assert.False(result.Output.ReadIndex.Hit);
        Assert.Contains("ops.catalog.json", result.Output.ReadIndex.FallbackReason, StringComparison.Ordinal);
        PlanServiceInvocationAssert.PlanDispatched(unityIpcRequestExecutor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightFailsWithReadIndexError_ReturnsFailureWithoutCallingUnity ()
    {
        var service = CreateService(
            staticPreflightService: new RecordingRequestStaticValidationPreflightService
            {
                Result = RequestStaticValidationPreflightResult.Failure(
                    ExecutionError.InternalError("readIndexMode=requireFresh requires index freshness 'fresh'."),
                    CreatePreparedRequestContext(),
                    CreateReadIndexInfo(
                        used: true,
                        hit: true,
                        freshness: IndexFreshness.Stale,
                        fallbackReason: "readIndexMode=requireFresh requires index freshness 'fresh'."),
                    ReadIndexErrorCodes.ReadIndexFreshRequired),
            },
            unityRequestExecutor: new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(readIndexMode: ReadIndexMode.RequireFresh),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.Equal(RequestId, result.Output!.RequestId);
        Assert.True(result.Output.ReadIndex.Used);
        Assert.True(result.Output.ReadIndex.Hit);
        Assert.Equal(IndexFreshness.Stale, result.Output.ReadIndex.Freshness);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFreshRequired, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightFailsWithInvalidArgument_ReturnsFailureWithoutOutput ()
    {
        var service = CreateService(
            staticPreflightService: new RecordingRequestStaticValidationPreflightService
            {
                Result = RequestStaticValidationPreflightResult.Failure(
                    ExecutionError.InvalidArgument("readIndexMode is invalid.")),
            },
            unityRequestExecutor: new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(readIndexMode: ReadIndexMode.RequireFresh),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Null(result.Output);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightHasValidationErrors_ReturnsInvalidArgumentWithoutCallingUnity ()
    {
        ValidationError[] validationErrors =
        [
            new ValidationError(
                ValidationErrorCodes.OperationArgsInvalid,
                "Operation args are invalid.",
                new IpcExecuteStepId("step-1")),
        ];
        var service = CreateService(
            staticPreflightService: new RecordingRequestStaticValidationPreflightService
            {
                Result = RequestStaticValidationPreflightResult.ValidationFailure(
                    CreatePreparedRequestContext(),
                    CreateReadIndexInfo(
                        used: true,
                        hit: true,
                        freshness: IndexFreshness.Probable,
                        fallbackReason: null),
                    validationErrors),
            },
            unityRequestExecutor: new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.NotNull(result.Output);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCodes.OperationArgsInvalid, error.Code);
    }
}
