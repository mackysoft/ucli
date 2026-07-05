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
        var staticValidationService = new RecordingRequestStaticValidationService
        {
            Result = ValidationResult.Success(),
        };
        var service = CreateService(
            staticPreflightService: staticPreflightService,
            staticValidationService: staticValidationService,
            unityRequestExecutor: unityIpcRequestExecutor);

        var result = await service.ExecuteAsync(
            CreateInput(
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1234,
                failFast: true,
                allowPlayMode: true),
            CancellationToken.None);

        PlanServiceInvocationAssert.AllowPlayModeUsedLiveStaticValidation(
            result,
            staticPreflightService,
            staticValidationService);
        PlanServiceInvocationAssert.PlanDispatched(
            unityIpcRequestExecutor,
            UnityExecutionMode.Oneshot,
            TimeSpan.FromMilliseconds(1234),
            expectedFailFast: true,
            expectedAllowPlayMode: true,
            expectedRequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAllowPlayModeAndReadIndexModeAreSpecified_ReturnsInvalidArgumentWithoutPreflight ()
    {
        var staticPreflightService = CreateSuccessfulPreflightService();
        var staticValidationService = new RecordingRequestStaticValidationService
        {
            Result = ValidationResult.Success(),
        };
        var service = CreateService(
            staticPreflightService: staticPreflightService,
            staticValidationService: staticValidationService,
            unityRequestExecutor: new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
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
            staticValidationService);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAllowPlayModeStaticValidationFails_ReturnsFailureWithoutCallingPlan ()
    {
        ValidationError[] validationErrors =
        [
            new ValidationError(
                OperationAuthorizationErrorCodes.OperationNotAllowed,
                "Edit step 'step-1' requires operation 'ucli.comp.set'. Operation is blocked.",
                "step-1"),
        ];
        var service = CreateService(
            staticPreflightService: CreateSuccessfulPreflightService(),
            staticValidationService: new RecordingRequestStaticValidationService
            {
                Result = new ValidationResult(validationErrors),
            },
            unityRequestExecutor: new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
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
