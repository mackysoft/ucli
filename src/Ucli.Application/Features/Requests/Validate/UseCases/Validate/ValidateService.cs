using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Validate.UseCases.Validate;

/// <summary> Implements the <c>validate</c> workflow as snapshot-based static linting. </summary>
internal sealed class ValidateService : IValidateService
{
    private readonly IRequestPreparationService requestPreparationService;

    private readonly IRequestStaticValidator requestStaticValidator;

    private readonly IRequestStaticValidationPreflightService requestStaticValidationPreflightService;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="ValidateService" /> class. </summary>
    /// <param name="requestPreparationService"> The shared request-preparation dependency. </param>
    /// <param name="requestStaticValidator"> The static-validator dependency. </param>
    /// <param name="requestStaticValidationPreflightService"> The shared static-validation preflight dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout cancellation. </param>
    public ValidateService (
        IRequestPreparationService requestPreparationService,
        IRequestStaticValidator requestStaticValidator,
        IRequestStaticValidationPreflightService requestStaticValidationPreflightService,
        TimeProvider timeProvider)
    {
        this.requestPreparationService = requestPreparationService ?? throw new ArgumentNullException(nameof(requestPreparationService));
        this.requestStaticValidator = requestStaticValidator ?? throw new ArgumentNullException(nameof(requestStaticValidator));
        this.requestStaticValidationPreflightService = requestStaticValidationPreflightService ?? throw new ArgumentNullException(nameof(requestStaticValidationPreflightService));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async ValueTask<ValidateServiceResult> ExecuteAsync (
        ValidateCommandInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

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

        var preparedRequest = requestPreparationResult.PreparedRequest!;
        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Validate,
            preparedRequest.ProjectContext.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            var error = timeoutResolutionResult.Error!;
            return ValidateServiceResult.Failure(
                error.Message,
                ExecutionErrorCodeMapper.ToCode(error));
        }

        var timeout = timeoutResolutionResult.Timeout!.Value;
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        if (input.ReadIndexMode == ReadIndexMode.Disabled)
        {
            var disabledOutput = new ValidateExecutionOutput(
                Project: ProjectIdentityInfo.From(preparedRequest.ProjectContext.UnityProject),
                ReadIndex: CreateReadIndexDisabledOutput());
            if (!deadline.TryGetRemainingTimeout(out var validationTimeout))
            {
                return ValidateServiceResult.Failure(
                    CreateTimeoutFailureMessage(timeout),
                    ExecutionErrorCodes.IpcTimeout,
                    disabledOutput);
            }

            ValidationResult disabledValidationResult;
            using (var timeoutCancellationScope = TimeProviderCancellationScope.CreateLinked(cancellationToken, validationTimeout, timeProvider))
            {
                try
                {
                    disabledValidationResult = await requestStaticValidator.ValidateAsync(
                            preparedRequest.Request,
                            RequestStaticValidationCatalog.Unavailable,
                            preparedRequest.ProjectContext.Config,
                            timeoutCancellationScope.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (timeoutCancellationScope.HasTimedOut
                    && !cancellationToken.IsCancellationRequested)
                {
                    return ValidateServiceResult.Failure(
                        CreateTimeoutFailureMessage(timeout),
                        ExecutionErrorCodes.IpcTimeout,
                        disabledOutput);
                }
            }

            if (deadline.IsExpired)
            {
                return ValidateServiceResult.Failure(
                    CreateTimeoutFailureMessage(timeout),
                    ExecutionErrorCodes.IpcTimeout,
                    disabledOutput);
            }

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

        if (!deadline.TryGetRemainingTimeout(out var preflightTimeout))
        {
            return ValidateServiceResult.Failure(
                CreateTimeoutFailureMessage(timeout),
                ExecutionErrorCodes.IpcTimeout);
        }

        RequestStaticValidationPreflightResult requestStaticValidationPreflightResult;
        using (var timeoutCancellationScope = TimeProviderCancellationScope.CreateLinked(cancellationToken, preflightTimeout, timeProvider))
        {
            try
            {
                requestStaticValidationPreflightResult = await requestStaticValidationPreflightService.PrepareAsync(
                        preparedRequest,
                        input.ReadIndexMode,
                        timeoutCancellationScope.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (timeoutCancellationScope.HasTimedOut
                && !cancellationToken.IsCancellationRequested)
            {
                return ValidateServiceResult.Failure(
                    CreateTimeoutFailureMessage(timeout),
                    ExecutionErrorCodes.IpcTimeout);
            }
        }

        if (deadline.IsExpired)
        {
            return ValidateServiceResult.Failure(
                CreateTimeoutFailureMessage(timeout),
                ExecutionErrorCodes.IpcTimeout);
        }

        var output = requestStaticValidationPreflightResult.ReadIndex != null
            ? new ValidateExecutionOutput(
                Project: ProjectIdentityInfo.From(preparedRequest.ProjectContext.UnityProject),
                ReadIndex: requestStaticValidationPreflightResult.ReadIndex)
            : null;
        if (requestStaticValidationPreflightResult.Error != null)
        {
            return ValidateServiceResult.Failure(
                requestStaticValidationPreflightResult.Error.Message,
                requestStaticValidationPreflightResult.ErrorCode!,
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

    private static string CreateTimeoutFailureMessage (TimeSpan timeout)
    {
        return $"Validate timed out after {timeout.TotalMilliseconds:0} milliseconds.";
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
