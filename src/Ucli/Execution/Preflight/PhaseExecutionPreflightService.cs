using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;

namespace MackySoft.Ucli.Execution;

/// <summary> Executes request preflight for phase-based command execution. </summary>
internal sealed class PhaseExecutionPreflightService : IPhaseExecutionPreflightService
{
    private static readonly IReadOnlyDictionary<string, UcliOperationDescriptor> EmptyOperationsByName
        = new Dictionary<string, UcliOperationDescriptor>(0, StringComparer.Ordinal);

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
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The preflight result. </returns>
    public async ValueTask<PhaseExecutionPreflightResult> Prepare (
        PreparedRequestContext preparedRequest,
        UnityExecutionMode mode,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(preparedRequest);

        if (!deadline.TryGetRemainingTimeout(out var operationCatalogTimeout))
        {
            return PhaseExecutionPreflightResult.Failure(
                ExecutionError.Timeout("Timed out before operation metadata discovery could begin."),
                CreatePreparedRequest(preparedRequest, EmptyOperationsByName));
        }

        IReadOnlyList<UcliOperationDescriptor> operations;
        try
        {
            operations = await operationCatalog.GetAll(
                    preparedRequest.ProjectContext.UnityProject,
                    preparedRequest.ProjectContext.Config,
                    mode,
                    operationCatalogTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCatalogLoadException exception)
        {
            return PhaseExecutionPreflightResult.Failure(
                exception.CreatePrefixedError("Static validation could not load operation metadata."),
                CreatePreparedRequest(preparedRequest, EmptyOperationsByName),
                exception.ErrorCode);
        }
        catch (InvalidOperationException exception)
        {
            return PhaseExecutionPreflightResult.Failure(
                ExecutionError.InternalError($"Static validation could not load operation metadata. {exception.Message}"),
                CreatePreparedRequest(preparedRequest, EmptyOperationsByName));
        }

        var operationsByName = new Dictionary<string, UcliOperationDescriptor>(operations.Count, StringComparer.Ordinal);
        for (var i = 0; i < operations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var operation = operations[i];
            operationsByName[operation.Name] = operation;
        }

        var phasePreparedRequest = CreatePreparedRequest(preparedRequest, operationsByName);
        var validationResult = await requestStaticValidator.Validate(
                preparedRequest.Request,
                RequestStaticValidationCatalog.Available(operations),
                preparedRequest.ProjectContext.Config,
                cancellationToken)
            .ConfigureAwait(false);
        if (validationResult.Error != null)
        {
            return PhaseExecutionPreflightResult.Failure(validationResult.Error, phasePreparedRequest);
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