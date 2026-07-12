using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Validate.UseCases.Validate;
using MackySoft.Ucli.Contracts.Configuration;
using static MackySoft.Ucli.Application.Tests.ValidateServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ValidateServiceReadIndexDisabledTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExplicitReadIndexModeIsDisabled_RequiresProjectPreparationAndSkipsSharedPreflight ()
    {
        var requestPreparationService = CreateRequestPreparationService(
            RequestPreparationResult.Success(CreatePreparedRequestContext()));
        var validator = new RecordingRequestStaticValidator
        {
            Result = ValidationResult.Success(),
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
            requestPreparationService,
            validator,
            preflightService);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput(null, ReadIndexMode.Disabled, """{"steps":[]}"""),
            CancellationToken.None);

        var output = ValidateServiceAssert.ReadIndexDisabledSuccessReturnedWithoutSharedPreflight(
            result,
            preflightService);
        RequestStaticValidationInvocationAssert.PureStaticValidationRequestedOnce(
            validator,
            expectedCatalogAvailable: false);
        Assert.Equal("/tmp/project", output.Project.ProjectPath);
        RequestPreparationInvocationAssert.ProjectPreparedOnce(
            requestPreparationService,
            expectedProjectPath: null,
            expectedRequestJson: """{"steps":[]}""");
    }
}
