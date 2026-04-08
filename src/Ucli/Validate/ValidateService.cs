using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.ReadIndex;

namespace MackySoft.Ucli.Validate;

/// <summary> Implements the <c>validate</c> workflow as snapshot-based static linting. </summary>
internal sealed class ValidateService : IValidateService
{
    private readonly IRequestPreparationService requestPreparationService;

    private readonly IRequestStaticValidator requestStaticValidator;

    private readonly IValidateMetadataResolver validateMetadataResolver;

    /// <summary> Initializes a new instance of the <see cref="ValidateService" /> class. </summary>
    /// <param name="requestPreparationService"> The shared request-preparation dependency. </param>
    /// <param name="requestStaticValidator"> The static-validator dependency. </param>
    /// <param name="validateMetadataResolver"> The validate metadata resolver dependency. </param>
    public ValidateService (
        IRequestPreparationService requestPreparationService,
        IRequestStaticValidator requestStaticValidator,
        IValidateMetadataResolver validateMetadataResolver)
    {
        this.requestPreparationService = requestPreparationService ?? throw new ArgumentNullException(nameof(requestPreparationService));
        this.requestStaticValidator = requestStaticValidator ?? throw new ArgumentNullException(nameof(requestStaticValidator));
        this.validateMetadataResolver = validateMetadataResolver ?? throw new ArgumentNullException(nameof(validateMetadataResolver));
    }

    /// <inheritdoc />
    public async ValueTask<ValidateServiceResult> Execute (
        ValidateCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        var requestPreparationResult = await requestPreparationService.Prepare(
                input.RequestPath,
                input.ProjectPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!requestPreparationResult.IsSuccess)
        {
            var error = requestPreparationResult.Error!;
            return ValidateServiceResult.Failure(
                error.Message,
                ExecutionErrorKindCodeMapper.ToCode(error.Kind),
                output: null);
        }

        var preparedRequest = requestPreparationResult.PreparedRequest!;
        var readIndexModeResult = ReadIndexModeResolver.Resolve(input.ReadIndexMode, preparedRequest.ProjectContext.Config);
        if (!readIndexModeResult.IsSuccess)
        {
            var error = readIndexModeResult.Error!;
            return ValidateServiceResult.Failure(
                error.Message,
                ExecutionErrorKindCodeMapper.ToCode(error.Kind),
                output: null);
        }

        var metadataResult = await validateMetadataResolver.Resolve(
                preparedRequest.ProjectContext.UnityProject,
                readIndexModeResult.Mode!.Value,
                cancellationToken)
            .ConfigureAwait(false);
        var output = new ValidateExecutionOutput(metadataResult.ReadIndex);
        if (!metadataResult.IsSuccess)
        {
            return ValidateServiceResult.Failure(
                metadataResult.ErrorMessage!,
                metadataResult.ErrorCode!,
                output);
        }

        var validationResult = await requestStaticValidator.Validate(
                preparedRequest.Request,
                metadataResult.Catalog,
                preparedRequest.ProjectContext.Config,
                cancellationToken)
            .ConfigureAwait(false);
        if (validationResult.Error != null)
        {
            return ValidateServiceResult.Failure(
                validationResult.Error.Message,
                ExecutionErrorKindCodeMapper.ToCode(validationResult.Error.Kind),
                output);
        }

        if (!validationResult.IsValid)
        {
            return ValidateServiceResult.ValidationFailure(
                output,
                "Static validation failed.",
                validationResult.Errors);
        }

        return ValidateServiceResult.Success(output, "Static validation passed.");
    }
}