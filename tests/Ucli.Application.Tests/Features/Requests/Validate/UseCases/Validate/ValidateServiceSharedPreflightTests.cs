using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Validate.UseCases.Validate;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.ValidateServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ValidateServiceSharedPreflightTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSharedPreflightFails_ReturnsFailureWithReadIndexOutput ()
    {
        var preflightService = new RecordingRequestStaticValidationPreflightService
        {
            Result = RequestStaticValidationPreflightResult.Failure(
                ExecutionError.InternalError("Index contract file 'ops.catalog.json' is malformed."),
                CreatePreparedRequestContext(),
                CreateReadIndexInfo(
                    used: false,
                    hit: false,
                    freshness: IndexFreshness.Probable),
                ReadIndexErrorCodes.ReadIndexFormatInvalid),
        };
        var service = new ValidateService(
            CreateRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            },
            preflightService,
            TimeProvider.System);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        ValidateServiceAssert.SharedPreflightFailureReturnedWithReadIndexOutput(
            result,
            preflightService,
            ReadIndexErrorCodes.ReadIndexFormatInvalid,
            expectedReadIndexUsed: false);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSharedPreflightHasValidationErrors_ReturnsValidationFailure ()
    {
        ValidationError[] validationErrors =
        [
            new ValidationError(
                ValidationErrorCodes.OperationArgsInvalid,
                "Operation args are invalid.",
                new IpcExecuteStepId("step-1")),
        ];
        var preflightService = new RecordingRequestStaticValidationPreflightService
        {
            Result = RequestStaticValidationPreflightResult.ValidationFailure(
                CreatePreparedRequestContext(),
                CreateReadIndexInfo(
                    used: true,
                    hit: true,
                    freshness: IndexFreshness.Probable),
                validationErrors),
        };
        var service = new ValidateService(
            CreateRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            },
            preflightService,
            TimeProvider.System);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Output);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCodes.OperationArgsInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSharedPreflightSucceeds_ReturnsSuccess ()
    {
        var preflightService = new RecordingRequestStaticValidationPreflightService
        {
            Result = RequestStaticValidationPreflightResult.Success(
                CreatePreparedRequestContext(),
                CreateReadIndexInfo(
                    used: true,
                    hit: true,
                    freshness: IndexFreshness.Probable)),
        };
        var service = new ValidateService(
            CreateRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            },
            preflightService,
            TimeProvider.System);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        ValidateServiceAssert.SharedPreflightSuccessReturned(
            result,
            preflightService,
            expectedReadIndexUsed: true);
    }
}
