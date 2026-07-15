using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.Results;

public sealed class RequestServiceFailureOutcomeContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenFailureOutcomesAreMixed_ResolvesToolError ()
    {
        ApplicationFailure[] errors =
        [
            ApplicationFailure.InvalidInput("Invalid argument."),
            ApplicationFailure.ExternalProcessFailure(
                "Infrastructure failed.",
                outcome: ApplicationOutcome.InfrastructureError),
        ];

        Assert.Equal(ApplicationOutcome.ToolError, ApplicationFailureOutcomeResolver.Resolve(errors));

        var result = PlanServiceResult.Failure("Plan failed.", errors);

        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenOnlyInvalidInputFailuresExist_ResolvesInvalidArgument ()
    {
        ApplicationFailure[] errors =
        [
            ApplicationFailure.InvalidInput("Invalid argument."),
            ApplicationFailure.ConfigurationError("Configuration is invalid."),
        ];

        var result = CallServiceResult.Failure("Call failed.", errors);

        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenOnlyInfrastructureFailuresExist_ResolvesInfrastructureError ()
    {
        ApplicationFailure[] errors =
        [
            ApplicationFailure.ExternalProcessFailure(
                "Unity test infrastructure failed.",
                outcome: ApplicationOutcome.InfrastructureError),
        ];

        var result = OperationExecuteResultFactory.Failure(RequestServiceResultInvariantTestSupport.RequestId, [], errors);

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromExecutionErrorOverride_UsesFinalErrorCodeForOutcome ()
    {
        var result = PlanFailureResultFactory.FromExecutionError(
            ExecutionError.InternalError("Project path is invalid."),
            errorCode: UcliCoreErrorCodes.InvalidArgument);

        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ApplicationFailureKind.InvalidInput, error.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InvalidArgumentErrorCodes_MapToInvalidArgumentOutcome ()
    {
        foreach (UcliCode errorCode in RequestServiceResultInvariantTestSupport.InvalidArgumentErrorCodeValues)
        {
            var validationError = new ValidationError(
                errorCode,
                "Validation failed.",
                new IpcExecuteStepId("step-1"));

            Assert.True(InvalidArgumentErrorCodeSet.Contains(errorCode));

            var operationResult = OperationExecuteResultFactory.FromValidationErrors(
                RequestServiceResultInvariantTestSupport.RequestId,
                [
                    validationError,
                ]);
            Assert.Equal(ApplicationOutcome.InvalidArgument, operationResult.Outcome);
            Assert.Equal(errorCode, Assert.Single(operationResult.Errors).Code);

            var validateResult = ValidateServiceResult.ValidationFailure(
                new ValidateExecutionOutput(ProjectIdentityInfoTestFactory.Create(), RequestServiceResultInvariantTestSupport.CreateReadIndexInfo()),
                "Static validation failed.",
                [
                    validationError,
                ]);
            Assert.Equal(ApplicationOutcome.InvalidArgument, validateResult.Outcome);
            Assert.Equal(errorCode, Assert.Single(validateResult.Errors).Code);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnknownErrorCode_IsPreservedAndMapsToToolError ()
    {
        var futureErrorCode = new UcliCode("FUTURE_TRANSPORT_FAILURE");
        var error = RequestFailureNormalizer.FromTransportFailure(
            errorCode: futureErrorCode,
            message: "Future transport failed.");

        Assert.Equal(futureErrorCode, error.Code);
        Assert.Equal(ApplicationOutcome.ToolError, error.Outcome);
        Assert.False(InvalidArgumentErrorCodeSet.Contains(error.Code));
    }
}
