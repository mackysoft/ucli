using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan.Projection;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Hosting.Cli.Requests.Plan.Preflight;

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
    public async ValueTask<PlanCommandPreflightResult> PrepareAsync (
        string? projectPath,
        string requestJson,
        ReadIndexMode? readIndexMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requestPreparationResult = await requestPreparationService.PrepareAsync(
                projectPath,
                requestJson,
                cancellationToken)
            .ConfigureAwait(false);
        if (requestPreparationResult.Error != null)
        {
            return PlanCommandPreflightResult.Failure(
                PlanFailureResultFactory.FromExecutionError(requestPreparationResult.Error));
        }

        var preparedRequest = requestPreparationResult.PreparedRequest!;
        var requestStaticValidationPreflightResult = await requestStaticValidationPreflightService.PrepareAsync(
                preparedRequest,
                readIndexMode,
                cancellationToken)
            .ConfigureAwait(false);

        var output = PlanExecutionOutputFactory.TryCreateBase(
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
            PlanExecutionOutputFactory.CreateBase(
                requestStaticValidationPreflightResult.PreparedRequest!,
                requestStaticValidationPreflightResult.ReadIndex!));
    }
}
