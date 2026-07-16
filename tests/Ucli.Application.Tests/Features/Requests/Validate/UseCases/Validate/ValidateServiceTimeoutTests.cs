using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Validate.UseCases.Validate;
using MackySoft.Ucli.Contracts.Configuration;
using static MackySoft.Ucli.Application.Tests.ValidateServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ValidateServiceTimeoutTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExplicitTimeoutElapsesDuringSharedPreflight_ReturnsTimeoutFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var preflightService = new RecordingRequestStaticValidationPreflightService
        {
            Result = RequestStaticValidationPreflightResult.Success(
                CreatePreparedRequestContext(),
                CreateReadIndexInfo(
                    used: true,
                    hit: true,
                    freshness: IndexFreshness.Probable)),
            OnPrepare = cancellationToken =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(101));
                cancellationToken.ThrowIfCancellationRequested();
            },
        };
        var service = new ValidateService(
            CreateRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            },
            preflightService,
            timeProvider);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}""")
            {
                TimeoutMilliseconds = 100,
            },
            CancellationToken.None);

        ValidateServiceAssert.SharedPreflightTimedOutAfterPrepareAttempt(result, preflightService);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenConfigTimeoutElapsesDuringSharedPreflight_ReturnsTimeoutFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var config = CreateConfigWithValidateTimeout(timeoutMilliseconds: 100);
        var preparedRequest = CreatePreparedRequestContext(config);
        var preflightService = new RecordingRequestStaticValidationPreflightService
        {
            Result = RequestStaticValidationPreflightResult.Success(
                preparedRequest,
                CreateReadIndexInfo(
                    used: true,
                    hit: true,
                    freshness: IndexFreshness.Probable)),
            OnPrepare = cancellationToken =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(101));
                cancellationToken.ThrowIfCancellationRequested();
            },
        };
        var service = new ValidateService(
            CreateRequestPreparationService(RequestPreparationResult.Success(preparedRequest)),
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            },
            preflightService,
            timeProvider);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        ValidateServiceAssert.SharedPreflightTimedOutAfterPrepareAttempt(result, preflightService);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadIndexDisabledValidationTimesOut_ReturnsTimeoutFailureWithDisabledOutput ()
    {
        var timeProvider = new ManualTimeProvider();
        var validator = new RecordingRequestStaticValidator
        {
            Result = ValidationResult.Success(),
            OnValidate = cancellationToken =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(101));
                cancellationToken.ThrowIfCancellationRequested();
            },
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
            CreateRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            validator,
            preflightService,
            timeProvider);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", ReadIndexMode.Disabled, """{"steps":[]}""")
            {
                TimeoutMilliseconds = 100,
            },
            CancellationToken.None);

        ValidateServiceAssert.ReadIndexDisabledValidationTimedOutWithoutSharedPreflight(
            result,
            preflightService);
    }
}
