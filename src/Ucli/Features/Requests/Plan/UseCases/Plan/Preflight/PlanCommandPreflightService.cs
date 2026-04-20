using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan.Projection;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;

namespace MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan.Preflight;

/// <summary> Prepares base payload data for <c>plan</c> command failures that occur after option parsing. </summary>
internal sealed class PlanCommandPreflightService : IPlanCommandPreflightService
{
    private readonly IRequestPreparationService requestPreparationService;

    private readonly IRequestStaticValidationPreflightService requestStaticValidationPreflightService;

    /// <summary> Initializes a new instance of the <see cref="PlanCommandPreflightService" /> class. </summary>
    public PlanCommandPreflightService (
        IRequestPreparationService requestPreparationService,
        IRequestStaticValidationPreflightService requestStaticValidationPreflightService)
    {
        this.requestPreparationService = requestPreparationService ?? throw new ArgumentNullException(nameof(requestPreparationService));
        this.requestStaticValidationPreflightService = requestStaticValidationPreflightService ?? throw new ArgumentNullException(nameof(requestStaticValidationPreflightService));
    }

    /// <inheritdoc />
    public async ValueTask<PlanCommandPreflightResult> Prepare (
        string? requestPath,
        string? projectPath,
        ReadIndexMode? readIndexMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requestPreparationResult = await requestPreparationService.Prepare(
                requestPath,
                projectPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (requestPreparationResult.Error != null)
        {
            return PlanCommandPreflightResult.Failure(
                PlanFailureResultFactory.FromExecutionError(requestPreparationResult.Error));
        }

        var preparedRequest = requestPreparationResult.PreparedRequest
            ?? throw new InvalidOperationException("Prepared request must be available when request preparation succeeds.");
        var requestStaticValidationPreflightResult = await requestStaticValidationPreflightService.Prepare(
                preparedRequest,
                readIndexMode,
                cancellationToken)
            .ConfigureAwait(false);

        var output = PlanExecutionOutputFactory.CreateBase(
            requestStaticValidationPreflightResult.PreparedRequest,
            requestStaticValidationPreflightResult.ReadIndex);
        if (requestStaticValidationPreflightResult.Error != null)
        {
            return PlanCommandPreflightResult.Failure(
                PlanFailureResultFactory.FromExecutionError(
                    requestStaticValidationPreflightResult.Error,
                    output,
                    requestStaticValidationPreflightResult.ErrorCode));
        }

        if (requestStaticValidationPreflightResult.HasValidationErrors)
        {
            return PlanCommandPreflightResult.Failure(
                PlanFailureResultFactory.FromValidationErrors(
                    requestStaticValidationPreflightResult.ValidationErrors,
                    output));
        }

        return PlanCommandPreflightResult.Success(
            output ?? throw new InvalidOperationException("Plan preflight must produce a base payload when it succeeds."));
    }
}