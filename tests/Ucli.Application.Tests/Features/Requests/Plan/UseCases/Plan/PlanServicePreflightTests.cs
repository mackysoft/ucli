using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
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
                    CreatePreparedRequestContext(),
                    CreateReadIndexInfo(
                        used: false,
                        hit: false,
                        freshness: IndexFreshness.Probable,
                        fallbackReason: "Index contract file was not found: ops.catalog.json."),
                    RequestStaticValidationCatalog.Unavailable),
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
    public async Task Execute_WhenSyntaxOnlyFallbackContainsDirectOperation_BindsResponseToExplicitLiveCatalogSettings ()
    {
        var preparedRequest = CreateOpPreparedRequestContext();
        var operationDescriptor = CreateOperationDescriptor();
        var operationCatalog = new RecordingOperationCatalog
        {
            Operations = [operationDescriptor],
        };
        var staticValidator = new RecordingRequestStaticValidator
        {
            Result = ValidationResult.Success(),
        };
        var unityIpcRequestExecutor = new RecordingUnityRequestExecutor(CreatePlanSuccess(
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
                        used: false,
                        hit: false,
                        freshness: IndexFreshness.Probable,
                        fallbackReason: "Index contract file was not found: ops.catalog.json."),
                    RequestStaticValidationCatalog.Unavailable),
            },
            operationCatalog: operationCatalog,
            requestStaticValidator: staticValidator,
            unityRequestExecutor: unityIpcRequestExecutor,
            timeProvider: new FakeTimeProvider(DateTimeOffset.UnixEpoch));

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                mode: UnityExecutionMode.Daemon,
                timeoutMilliseconds: 1234,
                readIndexMode: ReadIndexMode.AllowStale,
                failFast: true) with
            {
                RequestJson = OpRequestJson,
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var catalogInvocation = Assert.Single(operationCatalog.ProjectGetAllInvocations);
        Assert.Equal(UnityExecutionMode.Daemon, catalogInvocation.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), catalogInvocation.Timeout);
        Assert.True(catalogInvocation.FailFast);
        Assert.Empty(staticValidator.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreflightFailsWithReadIndexError_ReturnsFailureWithoutCallingUnity ()
    {
        var service = CreateService(
            staticPreflightService: new RecordingRequestStaticValidationPreflightService
            {
                Result = RequestStaticValidationPreflightResult.Failure(
                    ExecutionError.InternalError(
                        "readIndexMode=requireFresh requires index freshness 'fresh'.",
                        ReadIndexErrorCodes.ReadIndexFreshRequired),
                    CreatePreparedRequestContext(),
                    CreateReadIndexInfo(
                        used: true,
                        hit: true,
                        freshness: IndexFreshness.Stale,
                        fallbackReason: "readIndexMode=requireFresh requires index freshness 'fresh'."),
                    RequestStaticValidationCatalog.Unavailable),
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
    public async Task Execute_WhenPreflightHasValidationErrors_ReturnsInvalidArgumentWithoutCallingUnity ()
    {
        ValidationError[] validationErrors =
        [
            new ValidationError(
                ValidationErrorCodes.OperationArgsInvalid,
                "Operation args are invalid.",
                "/steps/0/args"),
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
                    RequestStaticValidationCatalog.Unavailable,
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
