using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Validate.UseCases.Validate;

/// <summary> Implements the <c>validate</c> workflow as snapshot-based static linting. </summary>
internal sealed class ValidateService : IValidateService
{
    private readonly IRequestPreparationService requestPreparationService;

    private readonly IRequestStaticValidator requestStaticValidator;

    private readonly IRequestStaticValidationPreflightService requestStaticValidationPreflightService;

    /// <summary> Initializes a new instance of the <see cref="ValidateService" /> class. </summary>
    /// <param name="requestPreparationService"> The shared request-preparation dependency. </param>
    /// <param name="requestStaticValidator"> The static-validator dependency. </param>
    /// <param name="requestStaticValidationPreflightService"> The shared static-validation preflight dependency. </param>
    public ValidateService (
        IRequestPreparationService requestPreparationService,
        IRequestStaticValidator requestStaticValidator,
        IRequestStaticValidationPreflightService requestStaticValidationPreflightService)
    {
        this.requestPreparationService = requestPreparationService ?? throw new ArgumentNullException(nameof(requestPreparationService));
        this.requestStaticValidator = requestStaticValidator ?? throw new ArgumentNullException(nameof(requestStaticValidator));
        this.requestStaticValidationPreflightService = requestStaticValidationPreflightService ?? throw new ArgumentNullException(nameof(requestStaticValidationPreflightService));
    }

    /// <inheritdoc />
    public async ValueTask<ValidateServiceResult> ExecuteAsync (
        ValidateCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        if (input.ReadIndexMode == ReadIndexMode.Disabled)
        {
            var parsedRequestResult = requestPreparationService.Parse(input.RequestJson);
            if (!parsedRequestResult.IsSuccess)
            {
                var error = parsedRequestResult.Error!;
                return ValidateServiceResult.Failure(
                    error.Message,
                    ExecutionErrorCodeMapper.ToCode(error),
                    output: null);
            }

            var disabledOutput = new ValidateExecutionOutput(CreateReadIndexDisabledOutput());
            var disabledValidationResult = await requestStaticValidator.ValidateAsync(
                    parsedRequestResult.ParsedRequest!.Request,
                    RequestStaticValidationCatalog.Unavailable,
                    UcliConfig.CreateDefault(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (disabledValidationResult.Error != null)
            {
                return ValidateServiceResult.Failure(
                    disabledValidationResult.Error.Message,
                    ExecutionErrorCodeMapper.ToCode(disabledValidationResult.Error),
                    disabledOutput);
            }

            if (!disabledValidationResult.IsValid)
            {
                return ValidateServiceResult.ValidationFailure(
                    disabledOutput,
                    "Static validation failed.",
                    disabledValidationResult.Errors);
            }

            return ValidateServiceResult.Success(disabledOutput, "Static validation passed.");
        }

        var requestPreparationResult = await requestPreparationService.PrepareAsync(
                input.ProjectPath,
                input.RequestJson,
                cancellationToken)
            .ConfigureAwait(false);
        if (!requestPreparationResult.IsSuccess)
        {
            var error = requestPreparationResult.Error!;
            return ValidateServiceResult.Failure(
                error.Message,
                ExecutionErrorCodeMapper.ToCode(error),
                output: null);
        }

        var requestStaticValidationPreflightResult = await requestStaticValidationPreflightService.PrepareAsync(
                requestPreparationResult.PreparedRequest!,
                input.ReadIndexMode,
                cancellationToken)
            .ConfigureAwait(false);
        var output = requestStaticValidationPreflightResult.ReadIndex != null
            ? new ValidateExecutionOutput(requestStaticValidationPreflightResult.ReadIndex)
            : null;
        if (requestStaticValidationPreflightResult.Error != null)
        {
            return ValidateServiceResult.Failure(
                requestStaticValidationPreflightResult.Error.Message,
                requestStaticValidationPreflightResult.ErrorCode!.Value,
                output);
        }

        if (requestStaticValidationPreflightResult.HasValidationErrors)
        {
            return ValidateServiceResult.ValidationFailure(
                output,
                "Static validation failed.",
                requestStaticValidationPreflightResult.ValidationErrors);
        }

        return ValidateServiceResult.Success(output!, "Static validation passed.");
    }

    private static ReadIndexInfo CreateReadIndexDisabledOutput ()
    {
        return new ReadIndexInfo(
            Used: false,
            Hit: false,
            Source: ReadIndexInfoSource.Index,
            Freshness: IndexFreshness.Probable,
            GeneratedAtUtc: null,
            FallbackReason: "readIndex disabled by mode.");
    }
}
