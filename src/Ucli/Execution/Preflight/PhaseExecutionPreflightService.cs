using MackySoft.Ucli.Operations;

namespace MackySoft.Ucli.Execution;

/// <summary> Executes request preflight for phase-based command execution. </summary>
internal sealed class PhaseExecutionPreflightService : IPhaseExecutionPreflightService
{
    private readonly IRequestPreparationService requestPreparationService;

    private readonly IRequestStaticValidationService requestStaticValidationService;

    /// <summary> Initializes a new instance of the <see cref="PhaseExecutionPreflightService" /> class. </summary>
    /// <param name="requestPreparationService"> The shared request-preparation dependency. </param>
    /// <param name="requestStaticValidationService"> The authoritative static-validation dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
    public PhaseExecutionPreflightService (
        IRequestPreparationService requestPreparationService,
        IRequestStaticValidationService requestStaticValidationService)
    {
        this.requestPreparationService = requestPreparationService ?? throw new ArgumentNullException(nameof(requestPreparationService));
        this.requestStaticValidationService = requestStaticValidationService ?? throw new ArgumentNullException(nameof(requestStaticValidationService));
    }

    /// <summary> Executes preflight and returns a prepared request or structured errors. </summary>
    /// <param name="requestPath"> The optional request path from <c>--requestPath</c>. </param>
    /// <param name="projectPath"> The optional Unity project path from <c>--projectPath</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The preflight result. </returns>
    public async ValueTask<PhaseExecutionPreflightResult> Prepare (
        string? requestPath,
        string? projectPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requestPreparationResult = await requestPreparationService.Prepare(
                requestPath,
                projectPath,
                cancellationToken)
            .ConfigureAwait(false);
        if (!requestPreparationResult.IsSuccess)
        {
            return PhaseExecutionPreflightResult.Failure(requestPreparationResult.Error!);
        }

        var preparedRequest = requestPreparationResult.PreparedRequest!;
        var validationResult = await requestStaticValidationService.Validate(
                preparedRequest.Request,
                preparedRequest.ProjectContext,
                cancellationToken)
            .ConfigureAwait(false);
        if (validationResult.Error != null)
        {
            return PhaseExecutionPreflightResult.Failure(validationResult.Error);
        }

        if (!validationResult.IsValid)
        {
            return PhaseExecutionPreflightResult.ValidationFailure(validationResult.Errors);
        }

        return PhaseExecutionPreflightResult.Success(new PhaseExecutionPreparedRequest(
            RequestJson: preparedRequest.RequestJson,
            InputSource: preparedRequest.InputSource,
            Request: preparedRequest.Request,
            UnityProject: preparedRequest.ProjectContext.UnityProject,
            Config: preparedRequest.ProjectContext.Config,
            ConfigSource: preparedRequest.ProjectContext.ConfigSource));
    }
}