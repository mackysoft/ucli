using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

using static MackySoft.Ucli.Application.Tests.PlanServiceTestSupport;

public sealed class PlanServiceAllowPlayModeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAllowPlayModeIsSpecified_SkipsReadIndexPreflightAndUsesLiveStaticValidation ()
    {
        var unityIpcRequestExecutor = new RecordingUnityRequestExecutor(CreatePlanSuccess("plan-token-1"));
        var staticPreflightService = CreateSuccessfulPreflightService();
        var operationCatalog = new RecordingOperationCatalog
        {
            Operations = [],
        };
        var staticValidator = new RecordingRequestStaticValidator
        {
            Result = ValidationResult.Success(),
        };
        var service = CreateService(
            staticPreflightService: staticPreflightService,
            operationCatalog: operationCatalog,
            requestStaticValidator: staticValidator,
            unityRequestExecutor: unityIpcRequestExecutor,
            timeProvider: new ManualTimeProvider());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1234,
                failFast: true,
                allowPlayMode: true),
            CancellationToken.None);

        PlanServiceInvocationAssert.AllowPlayModeUsedLiveStaticValidation(
            result,
            staticPreflightService,
            operationCatalog,
            staticValidator);
        var catalogInvocation = Assert.Single(operationCatalog.ProjectGetAllInvocations);
        Assert.Equal(UnityExecutionMode.Oneshot, catalogInvocation.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), catalogInvocation.Timeout);
        Assert.True(catalogInvocation.FailFast);
        var execution = PlanServiceInvocationAssert.PlanDispatched(unityIpcRequestExecutor);
        Assert.Equal(UnityExecutionMode.Oneshot, execution.Invocation.Mode);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), execution.Invocation.Timeout);
        Assert.True(execution.Request.FailFast);
        Assert.True(execution.Request.AllowPlayMode);
        Assert.Null(execution.Request.PlanToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAllowPlayModeAndReadIndexModeAreSpecified_ReturnsInvalidArgumentWithoutPreflight ()
    {
        var staticPreflightService = CreateSuccessfulPreflightService();
        var operationCatalog = new RecordingOperationCatalog
        {
            Operations = [],
        };
        var staticValidator = new RecordingRequestStaticValidator
        {
            Result = ValidationResult.Success(),
        };
        var service = CreateService(
            staticPreflightService: staticPreflightService,
            operationCatalog: operationCatalog,
            requestStaticValidator: staticValidator,
            unityRequestExecutor: new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1234,
                readIndexMode: ReadIndexMode.Disabled,
                failFast: true,
                allowPlayMode: true),
            CancellationToken.None);

        PlanServiceInvocationAssert.ReadIndexModeRejectedBeforeStaticValidation(
            result,
            staticPreflightService,
            operationCatalog,
            staticValidator);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAllowPlayModeStaticValidationFails_ReturnsFailureWithoutCallingPlan ()
    {
        ValidationError[] validationErrors =
        [
            new ValidationError(
                OperationAuthorizationErrorCodes.OperationNotAllowed,
                "Edit step requires operation 'ucli.comp.set'. Operation is blocked.",
                "/steps/0"),
        ];
        var service = CreateService(
            staticPreflightService: CreateSuccessfulPreflightService(),
            operationCatalog: new RecordingOperationCatalog
            {
                Operations = [],
            },
            requestStaticValidator: new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Invalid(validationErrors),
            },
            unityRequestExecutor: new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            RequestId,
            CreateInput(allowPlayMode: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
        var error = Assert.Single(result.Errors);
        Assert.Equal(OperationAuthorizationErrorCodes.OperationNotAllowed, error.Code);
    }
}
