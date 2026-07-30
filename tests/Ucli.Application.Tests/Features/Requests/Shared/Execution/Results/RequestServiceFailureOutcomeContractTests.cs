using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Execution.Results;

public sealed class RequestServiceFailureOutcomeContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenFailureOutcomesAreMixed_ResolvesToolError ()
    {
        ApplicationFailure[] errors =
        [
            ApplicationFailure.InvalidInput(
                "Invalid argument.",
                UcliCoreErrorCodes.InvalidArgument,
                instancePath: null,
                startupFailure: null),
            ApplicationFailure.Create(
                ApplicationFailureKind.ExternalProcessFailure,
                "Infrastructure failed.",
                UcliCoreErrorCodes.InternalError,
                instancePath: null,
                outcome: ApplicationOutcome.InfrastructureError,
                startupFailure: null),
        ];

        var result = PlanServiceResult.Failure("Plan failed.", errors, output: null);

        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenOnlyInvalidInputFailuresExist_ResolvesInvalidArgument ()
    {
        ApplicationFailure[] errors =
        [
            ApplicationFailure.InvalidInput(
                "Invalid argument.",
                UcliCoreErrorCodes.InvalidArgument,
                instancePath: null,
                startupFailure: null),
            ApplicationFailure.ConfigurationError(
                "Configuration is invalid.",
                UcliCoreErrorCodes.InvalidArgument,
                instancePath: null),
        ];

        var result = CallServiceResult.Failure("Call failed.", errors, output: null);

        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenOnlyInfrastructureFailuresExist_ResolvesInfrastructureError ()
    {
        ApplicationFailure[] errors =
        [
            ApplicationFailure.Create(
                ApplicationFailureKind.ExternalProcessFailure,
                "Unity test infrastructure failed.",
                UcliCoreErrorCodes.InternalError,
                instancePath: null,
                outcome: ApplicationOutcome.InfrastructureError,
                startupFailure: null),
        ];

        var result = OperationExecuteResultFactory.Failure(
            RequestServiceResultInvariantTestSupport.RequestId,
            [],
            errors,
            contractViolations: [],
            readPostcondition: null,
            project: null,
            postReadSource: null);

        Assert.Equal(ApplicationOutcome.InfrastructureError, result.Outcome);
        Assert.Equal("Unity test infrastructure failed.", result.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_FromExecutionError_UsesFinalErrorCodeForOutcome ()
    {
        var result = PlanFailureResultFactory.FromExecutionError(
            ExecutionError.InternalError(
                "Project path is invalid.",
                UcliCoreErrorCodes.InvalidArgument),
            output: null);

        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ApplicationFailureKind.InvalidInput, error.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ValidationErrors_MapToInvalidArgumentOutcome ()
    {
        var validationError = new ValidationError(
            ValidationErrorCodes.OperationArgsInvalid,
            "Validation failed.",
            "/steps/0");

        var operationResult = OperationExecuteResultFactory.FromValidationErrors(
            RequestServiceResultInvariantTestSupport.RequestId,
            [
                validationError,
            ],
            project: null);
        Assert.Equal(ApplicationOutcome.InvalidArgument, operationResult.Outcome);
        Assert.Equal(
            ValidationErrorCodes.OperationArgsInvalid,
            Assert.Single(operationResult.Errors).Code);

        var validateResult = ValidateServiceResult.ValidationFailure(
            new ValidateExecutionOutput(ProjectIdentityInfoTestFactory.Create(), RequestServiceResultInvariantTestSupport.CreateReadIndexInfo()),
            "Static validation failed.",
            [
                validationError,
            ]);
        Assert.Equal(ApplicationOutcome.InvalidArgument, validateResult.Outcome);
        Assert.Equal(
            ValidationErrorCodes.OperationArgsInvalid,
            Assert.Single(validateResult.Errors).Code);
    }
}
