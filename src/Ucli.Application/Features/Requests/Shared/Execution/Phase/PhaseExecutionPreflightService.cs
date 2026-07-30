using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;

/// <summary> Executes request preflight for phase-based command execution. </summary>
internal sealed class PhaseExecutionPreflightService : IPhaseExecutionPreflightService
{
    private readonly IOperationCatalog operationCatalog;

    private readonly IRequestStaticValidator requestStaticValidator;

    /// <summary> Initializes a new instance of the <see cref="PhaseExecutionPreflightService" /> class. </summary>
    /// <param name="operationCatalog"> The authoritative operation-catalog dependency. </param>
    /// <param name="requestStaticValidator"> The static-validator dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
    public PhaseExecutionPreflightService (
        IOperationCatalog operationCatalog,
        IRequestStaticValidator requestStaticValidator)
    {
        this.operationCatalog = operationCatalog ?? throw new ArgumentNullException(nameof(operationCatalog));
        this.requestStaticValidator = requestStaticValidator ?? throw new ArgumentNullException(nameof(requestStaticValidator));
    }

    /// <summary> Executes preflight and returns a prepared request or structured errors. </summary>
    /// <param name="preparedRequest"> The request that has already been read, parsed, and bound to project context. </param>
    /// <param name="mode"> The optional Unity execution mode from the outer command. </param>
    /// <param name="deadline"> The shared timeout budget for the surrounding command execution. </param>
    /// <param name="failFast"> Whether operation metadata discovery should fail immediately instead of waiting for Unity readiness. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The preflight result. </returns>
    public async ValueTask<PhaseExecutionPreflightResult> PrepareAsync (
        PreparedRequestContext preparedRequest,
        UnityExecutionMode mode,
        ExecutionDeadline deadline,
        bool failFast,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(deadline);

        if (!deadline.TryGetRemainingTimeout(out var operationCatalogTimeout))
        {
            return PhaseExecutionPreflightResult.Failure(
                ApplicationFailure.Timeout(
                    "Timed out before operation metadata discovery could begin.",
                    ExecutionErrorCodes.IpcTimeout,
                    instancePath: null,
                    startupFailure: null),
                CreatePreparedRequest(
                    preparedRequest,
                    RequestStaticValidationCatalog.Unavailable.OperationsByName));
        }

        IReadOnlyList<UcliOperationDescriptor> operations;
        try
        {
            operations = await operationCatalog.GetAllAsync(
                    preparedRequest.ProjectContext.UnityProject,
                    preparedRequest.ProjectContext.Config,
                    mode,
                    operationCatalogTimeout,
                    failFast,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCatalogLoadException exception)
        {
            return PhaseExecutionPreflightResult.Failure(
                exception.CreatePrefixedFailure("Static validation could not load operation metadata."),
                CreatePreparedRequest(
                    preparedRequest,
                    RequestStaticValidationCatalog.Unavailable.OperationsByName));
        }
        catch (InvalidOperationException exception)
        {
            return PhaseExecutionPreflightResult.Failure(
                ApplicationFailure.InternalError(
                    $"Static validation could not load operation metadata. {exception.Message}",
                    UcliCoreErrorCodes.InternalError,
                    instancePath: null,
                    startupFailure: null),
                CreatePreparedRequest(
                    preparedRequest,
                    RequestStaticValidationCatalog.Unavailable.OperationsByName));
        }

        var validationCatalog = RequestStaticValidationCatalog.Available(operations);
        var phasePreparedRequest = CreatePreparedRequest(
            preparedRequest,
            validationCatalog.OperationsByName);
        var validationResult = await requestStaticValidator.ValidateAsync(
                preparedRequest.Request,
                validationCatalog,
                preparedRequest.ProjectContext.Config,
                cancellationToken)
            .ConfigureAwait(false);
        if (validationResult.Error != null)
        {
            return PhaseExecutionPreflightResult.Failure(
                ApplicationFailure.FromExecutionError(validationResult.Error),
                phasePreparedRequest);
        }

        if (!validationResult.IsValid)
        {
            return PhaseExecutionPreflightResult.ValidationFailure(phasePreparedRequest, validationResult.Errors);
        }

        return PhaseExecutionPreflightResult.Success(phasePreparedRequest);
    }

    private static PhaseExecutionPreparedRequest CreatePreparedRequest (
        PreparedRequestContext preparedRequest,
        IReadOnlyDictionary<string, UcliOperationDescriptor> operationsByName)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(operationsByName);

        return new PhaseExecutionPreparedRequest(
            PreparedRequest: preparedRequest,
            OperationsByName: operationsByName);
    }
}
