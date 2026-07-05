using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Validate.UseCases.Validate;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Foundation;
using static MackySoft.Ucli.Application.Tests.ValidateServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ValidateServicePreparationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenRequestPreparationFails_ReturnsFailureWithoutOutput ()
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
            CreateRequestPreparationService(RequestPreparationResult.Failure(ExecutionError.InvalidArgument("project path is invalid."))),
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            },
            preflightService);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        ValidateServiceAssert.PreparationFailureStoppedBeforeSharedPreflight(
            result,
            preflightService,
            UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenValidateTimeoutConfigIsInvalid_ReturnsFailureWithoutSharedPreflight ()
    {
        var timeoutOverrides = new Dictionary<string, int?>(UcliConfig.CreateDefault().IpcTimeoutMillisecondsByCommand, StringComparer.Ordinal)
        {
            [UcliCommandIds.Validate.Name] = 0,
        };
        var config = UcliConfig.CreateDefault() with
        {
            IpcTimeoutMillisecondsByCommand = timeoutOverrides,
        };
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
            CreateRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext(config))),
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            },
            preflightService);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        ValidateServiceAssert.InvalidTimeoutStoppedBeforeSharedPreflight(
            result,
            preflightService,
            expectedMessageFragment: "ipcTimeoutMillisecondsByCommand[validate]");
    }
}
